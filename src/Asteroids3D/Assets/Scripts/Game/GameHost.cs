using System;
using System.Collections;
using Balance;
using Cameras;
using Damage;
using Game.Audio;
using Game.Runs;
using Game.Travel;
using Substrate;
using Substrate.Presentation;
using Substrate.Sectors;
using Substrate.Sessions;
using Ships.Loadout;
using UI;
using UI.Hangar;
using UI.Recap;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Ships.Registry;
using Substrate.Services.Units;
using Substrate.Services.Objectives;
using Substrate.Services.Locales;

namespace Game
{
    /// <summary>
    /// Runs the interactive game over one session: hangar, sectors, extraction recaps, then final results.
    /// Owns the player rig, viewport, UI, music, run records and clock; the session composes, loads and unloads.
    /// Hull carries between sectors. Death frames the explosion while surviving ships keep moving.
    /// Presentation is read once alongside the session's snapshot.
    /// Shutdown finishes the current setup operation, releases input waits, then tears down the owned world.
    /// An optional jump profile enables protected travel with extraction UI held in hyperspace.
    /// The hangar, recap and restart stand in for Home Base and player progress.
    /// https://github.com/amindell11/astronomical-home/issues/295#issuecomment-5787867584
    /// </summary>
    [RequireComponent(typeof(UnitService))]
    [RequireComponent(typeof(ObjectiveService))]
    public class GameHost : MonoBehaviour
    {
        // Values are pinned: scenes serialize the numbers, so renumbering rewrites authored data.
        /// <summary>Session policy for what happens when the persistent player ship dies.</summary>
        public enum PlayerDeathBehavior { None = 0, RestartSector = 2 }

        [Header("Session")]
        [SerializeField] internal SessionProfile sessionProfile = new SessionProfile();

        [Tooltip("Prefab of the player and its HUD. The host builds its own copy once after session " +
                 "compose; it persists across sector restarts. Null → no player (spectator).")]
        [SerializeField] internal PlayerRig playerRig;

        [Header("View")]
        [Tooltip("Observer camera spawned once at session start and framed on the fleet; the player " +
                 "rig and the sector's modules are handed this instance.")]
        [SerializeField] internal ObserverCam observerCamPrefab;

        [Header("Splash")]
        [Tooltip("Full-screen splash shown over the non-interactive steps (boot, session compose, " +
                 "sector load/unload). Optional; skipped when presentation is off (headless/RL).")]
        [SerializeField] private LoadingSplash splashPrefab;

        [Header("Music")]
        [Tooltip("The run's music, built once for the session and told each run step's state. Optional; " +
                 "skipped when presentation is off (headless/RL).")]
        [SerializeField] internal GameMusic musicPrefab;

        [Header("Hangar")]
        [Tooltip("Between-run hangar screen. Null → no interactive hangar (the standing loadout is " +
                 "applied silently).")]
        [SerializeField] internal HangarScreen hangarScreenPrefab;

        [Tooltip("Camera that shows the sky behind the hangar in place of the observer camera, drifting " +
                 "so its layers parallax. Null → the hangar shows the observer camera's view. Skipped " +
                 "when presentation is off.")]
        [SerializeField] internal DriftCam hangarCameraPrefab;

        [Tooltip("Modules the hangar offers per slot. Null → no hangar choices (the player flies its " +
                 "prefab-authored modules).")]
        [SerializeField] internal ItemSubset hangarOffer;

        [Header("Recap")]
        [Tooltip("Timings of the recap sequence: HUD fade, banner, gap, breakdown. The preview host tunes " +
                 "this same asset. Required whenever presentation is on.")]
        [SerializeField] internal RecapFeelProfile recapFeel;

        [Header("Sector travel")]
        [SerializeField] internal SectorJumpProfile jumpFeel;
        [SerializeField] internal Shader jumpShader;

        [Header("Death Policy")]
        [Tooltip("What happens when the player ship dies. RestartSector ends the run (run record, death " +
                 "recap) and starts a new one from the hangar; None does nothing.")]
        [SerializeField] private PlayerDeathBehavior deathBehavior = PlayerDeathBehavior.RestartSector;

        [Header("Kill Refill")]
        [Tooltip("Fraction of max hull restored to the player on each kill the run tally counts.")]
        [SerializeField, Range(0f, 1f)] private float killHullRestore = 0.25f;

        internal string runRecordPath;
        internal BuildIdentitySource buildIdentity;

        private RunRecordStore runRecords;
        private DamageInfo lastKillingBlow;
        private Vector2 deathPosition;
        private bool playerDied;
        private bool sectorEnded;
        private float sectorEndTime;
        private float? recapTimeScale;
        private bool shutdownRequested;
        private bool shutdownComplete;
        private int activeSectorIndex;
        private SectorJumpFlight jump;

        private UnitService unitService;
        private ObjectiveService objectiveService;

        private Session session;
        private ObserverCam observer;
        private DriftCam hangarCamera;
        private PlayerRig rigInstance;
        private LoadingSplash splash;
        private GameMusic music;

        private Transform viewport;
        private Transform ui;
        private Transform arena;

        public Sector ActiveSector => session?.ActiveSector;
        public SectorJumpFlight Jump => jump;

        private void Awake()
        {
            unitService = GetComponent<UnitService>();
            objectiveService = GetComponent<ObjectiveService>();

            viewport = NewRoot("Viewport");
            ui = NewRoot("UI");
            arena = NewRoot("Arena");

            buildIdentity ??= BuildIdentity.Source();
            runRecords = new RunRecordStore(runRecordPath);

            DontDestroyOnLoad(gameObject);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            // No yield separates this read from the session's own snapshot, so the two cannot disagree.
            var presentation = sessionProfile.presentation;
            if (presentation && !recapFeel)
                throw new InvalidOperationException("GameHost presents but has no recap feel profile assigned.");
            if (presentation && jumpFeel && !jumpShader)
                throw new InvalidOperationException("GameHost enables jump travel but has no jump shader assigned.");
            session = new Session(sessionProfile, arena, unitService, objectiveService);
            if (presentation)
                BuildEventSystem(ui);
            if (splashPrefab && presentation)
                splash = Instantiate(splashPrefab, ui);
            if (musicPrefab && presentation)
            {
                music = Instantiate(musicPrefab, transform);
                music.Initialize(objectiveService);
            }

            SetSplashVisible(true);
            yield return null;

            yield return session.Compose();
            observer = BuildObserver(session.Units, presentation, viewport);
            if (hangarCameraPrefab && presentation)
            {
                hangarCamera = Instantiate(hangarCameraPrefab, viewport);
                SetHangarViewVisible(false);
            }
            if (playerRig)
            {
                // An asset gets no lifecycle, and state parked on it would outlive the session.
                rigInstance = Instantiate(playerRig, transform);
                yield return rigInstance.Build(session.Units, session.Objectives, presentation,
                    observer, ui, session.Frame, BuildDeathCallback());
                rigInstance.Tally.Killed += RefillPlayerHull;
            }

            yield return PlayRuns(presentation);

            if (jump)
            {
                jump.Release(false);
                Destroy(jump.gameObject);
                jump = null;
                yield return null;
            }

            if (rigInstance)
            {
                rigInstance.Tally.Killed -= RefillPlayerHull;
                rigInstance.Teardown();
            }
            RestoreRecapClock();
            yield return session.Teardown();
            if (rigInstance) Destroy(rigInstance.gameObject);
            if (music) Destroy(music.gameObject);
            Destroy(ui.gameObject);
            Destroy(viewport.gameObject);
            Destroy(arena.gameObject);
            yield return null;
            shutdownComplete = true;
        }

        public IEnumerator Shutdown()
        {
            shutdownRequested = true;
            while (!shutdownComplete) yield return null;
        }

        private IEnumerator PlayRuns(bool presentation)
        {
            while (!shutdownRequested)
            {
                SetSplashVisible(false);
                if (rigInstance)
                {
                    SetMusic(GameMusic.State.Hangar);
                    SetHangarViewVisible(true);
                    yield return RunHangar(rigInstance, presentation, ui);
                    SetHangarViewVisible(false);
                }
                if (shutdownRequested) yield break;

                playerDied = false;
                string playerLoadoutStatHash = null;
                var hull = 1f;
                var index = 0;
                var transferred = false;
                while (true)
                {
                    if (!transferred)
                    {
                        SetSplashVisible(true);
                        sectorEnded = false;
                        activeSectorIndex = index;
                        if (rigInstance) rigInstance.Spawns.BeginSector();
                        yield return session.LoadSector(index, rigInstance ? rigInstance.Player : null, OnSectorComplete);
                    }
                    transferred = false;
                    if (shutdownRequested) yield break;
                    if (index == 0)
                        playerLoadoutStatHash = rigInstance ? BeginRun() : null;
                    else if (rigInstance)
                        rigInstance.Player.Damage.Health.SetFraction(hull);
                    SetSplashVisible(false);
                    if (music) music.EnterSector(session.ActiveSector);
                    var sectorStartTime = Time.time;

                    while (!playerDied && !sectorEnded && !shutdownRequested)
                        yield return null;
                    if (shutdownRequested) yield break;

                    if (playerDied || index == sessionProfile.sectors.Length - 1) break;

                    // The unload despawns this sector's ships, and the next load resets the player's hull.
                    if (rigInstance)
                    {
                        hull = rigInstance.Player.Damage.Health.Pct;
                        rigInstance.Spawns.EndSector(sectorEndTime);
                        SetMusic(GameMusic.State.Recap);
                        if (presentation)
                        {
                            var extraction = RecapOutcomeContext.Extraction(
                                rigInstance.Spawns.KilledShips(currentSector: true), index + 1,
                                Mathf.Max(0f, sectorEndTime - sectorStartTime));
                            if (jump)
                            {
                                yield return RunJump(extraction, index + 1, hull);
                                transferred = true;
                            }
                            else yield return RunRecap(extraction);
                        }
                    }
                    if (shutdownRequested) yield break;
                    if (!transferred)
                    {
                        SetSplashVisible(true);
                        yield return session.UnloadSector();
                    }
                    if (shutdownRequested) yield break;
                    index++;
                }

                if (rigInstance)
                {
                    if (!playerDied) EndRun();
                    var board = AppendRunRecord(playerLoadoutStatHash, index, playerDied ? lastKillingBlow : null);
                    SetMusic(playerDied ? GameMusic.State.Defeat : GameMusic.State.Victory);
                    if (presentation)
                        yield return RunRecap(OutcomeContext(board, index));
                }
                if (shutdownRequested) yield break;

                SetSplashVisible(true);
                yield return session.UnloadSector();
                if (rigInstance) rigInstance.Park();
            }
        }

        private void OnSectorComplete(SectorResult result)
        {
            sectorEndTime = Time.time;
            sectorEnded = true;
            if (!jumpFeel || !sessionProfile.presentation || !rigInstance ||
                activeSectorIndex == sessionProfile.sectors.Length - 1) return;
            if (!result.Success || !result.DepartureHeading.HasValue)
                throw new InvalidOperationException("Jump-enabled extraction needs a successful result with a departure heading.");
            jump = SectorJumpFlight.Begin(transform, rigInstance, session.Units, observer, jumpFeel,
                jumpShader, result.DepartureHeading.Value);
        }

        private IEnumerator RunJump(RecapOutcomeContext context, int destination, float hull)
        {
            LocaleBackdropPair backdrop = null;
            yield return session.PrepareTravelBackdrop(sessionProfile.sectors[destination].config.Locale.SceneName,
                transform, pair => backdrop = pair);
            if (!shutdownRequested)
            {
                jump.BindBackdrop(ui, backdrop);
                while (!jump.Holding && !shutdownRequested) yield return null;
            }
            if (!shutdownRequested)
            {
                PauseRecapClock();
                yield return RecapPlayback.RunScreen(new RecapOutcomeSequence(recapFeel, context), ui,
                    cancelled: () => shutdownRequested);
            }
            if (!shutdownRequested)
            {
                sectorEnded = false;
                activeSectorIndex = destination;
                rigInstance.Spawns.BeginSector();
                yield return session.TransferSector(destination, rigInstance.Player, OnSectorComplete);
                rigInstance.Player.Damage.Health.SetFraction(hull);
            }
            if (!shutdownRequested)
            {
                jump.ResumeArrival();
                while (!jump.Complete && !shutdownRequested) yield return null;
            }
            jump.Release(!shutdownRequested);
            Destroy(jump.gameObject);
            jump = null;
            Destroy(backdrop.gameObject);
            RestoreRecapClock();
            yield return null;
        }

        private Transform NewRoot(string name)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            return root;
        }

        // The hangar and recap screens click through uGUI, which needs exactly one EventSystem.
        private static void BuildEventSystem(Transform parent)
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(parent, false);
        }

        private void SetSplashVisible(bool visible)
        {
            if (splash) splash.SetVisible(visible);
        }

        private void SetMusic(GameMusic.State state)
        {
            if (music) music.Set(state);
        }

        private void SetHangarViewVisible(bool visible)
        {
            if (!hangarCamera) return;
            hangarCamera.gameObject.SetActive(visible);
            observer.SetVisible(!visible);
        }

        /// <summary>Stays callable without a session so the presentation gate can be driven directly.</summary>
        internal ObserverCam BuildObserver(IUnitService units, bool presentationEnabled, Transform parent)
        {
            var built = Instantiate(observerCamPrefab, parent);

            // The authored prefab sees the locale's Sky layer; presentation-off does not draw it.
            if (!presentationEnabled)
                built.Cam.cullingMask &= ~(1 << LayerIds.Sky);

            // The camera carries authored presentation of its own (the reverb zone).
            PresentationApplier.Apply(built.gameObject, presentationEnabled);

            var registry = units.ActiveRegistry;
            if (registry == null)
                return built;

            registry.ActiveShips.OnAdd += s => built.AddSecondarySubject(s.transform);
            registry.ActiveShips.OnRemove += s => built.RemoveSecondarySubject(s.transform);
            return built;
        }

        private Action<ShipId, DamageInfo> BuildDeathCallback()
        {
            switch (deathBehavior)
            {
                case PlayerDeathBehavior.RestartSector:
                    return (_, killingBlow) =>
                    {
                        EndRun();
                        lastKillingBlow = killingBlow;
                        deathPosition = GamePlane.WorldPointToPlane(rigInstance.Player.transform.position);
                        playerDied = true;
                    };
                case PlayerDeathBehavior.None:
                default:
                    return null;
            }
        }

        /// <summary>Starts the rig's run clocks and returns the loadout stat hash the player flies this run.</summary>
        private string BeginRun()
        {
            rigInstance.Tally.Begin(Time.time);
            rigInstance.Spawns.Begin(Time.time);
            return StatHash.OfLoadout(rigInstance.Player);
        }

        private void EndRun()
        {
            rigInstance.Tally.End(Time.time);
            rigInstance.Spawns.End(Time.time);
        }

        // An unreadable build identity leaves the run unrecorded, like a failed write.
        private Scoreboard AppendRunRecord(string playerLoadoutStatHash, int endedIn, DamageInfo? killingBlow)
        {
            if (!buildIdentity(out var identity, out var failure))
            {
                Debug.LogError($"Run record skipped, build identity unreadable: {failure}", this);
                return Scoreboard.Read(runRecords.Path, null);
            }

            var sectors = Array.ConvertAll(sessionProfile.sectors, entry => entry.prefab);
            var sectorsCleared = killingBlow.HasValue ? endedIn : endedIn + 1;
            var record = RunRecord.Compose(DateTime.UtcNow, sectorsCleared, sectors[endedIn].name, identity,
                StatHash.OfSetting(hangarOffer, sectors, killHullRestore), rigInstance.Loadout, playerLoadoutStatHash,
                rigInstance.Tally, rigInstance.Ledger, rigInstance.Spawns, killingBlow);
            var board = Scoreboard.Read(runRecords.Path, record);
            return runRecords.Append(record) ? board : Scoreboard.Read(runRecords.Path, null);
        }

        private void RefillPlayerHull() => rigInstance.Player.Damage.Health.RestoreFraction(killHullRestore);

        /// <summary>Never blocks on a click when not presenting; callable without a session for tests.</summary>
        internal IEnumerator RunHangar(PlayerRig rig, bool presentationEnabled, Transform uiRoot)
        {
            if (!rig || !rig.Player || rig.Loadout == null || !hangarScreenPrefab || !presentationEnabled)
            {
                if (rig) rig.ApplyLoadout();
                yield break;
            }

            var screen = Instantiate(hangarScreenPrefab, uiRoot);
            var launched = false;
            screen.Show(hangarOffer, rig.Loadout, () => launched = true);

            yield return new WaitUntil(() => launched || shutdownRequested);

            if (!shutdownRequested) rig.ApplyLoadout();
            Destroy(screen.gameObject);
        }

        private RecapOutcomeContext OutcomeContext(Scoreboard board, int index)
        {
            var kills = rigInstance.Spawns.KilledShips();
            var count = sessionProfile.sectors.Length;
            return playerDied
                ? RecapOutcomeContext.Defeat(kills, index, count, board, deathPosition)
                : RecapOutcomeContext.Victory(kills, index + 1, count, board);
        }

        private IEnumerator RunRecap(RecapOutcomeContext context)
        {
            var sequence = new RecapOutcomeSequence(recapFeel, context);
            var presenter = new RecapPresenter(context, rigInstance.Overlay, session.Units.ActiveRegistry.ActiveShips,
                observer);
            try
            {
                yield return RecapPlayback.RunScreen(sequence, ui, state =>
                    {
                        presenter.Apply(state);
                        SetRecapClock(state.SimulationFrozen);
                    }, () => shutdownRequested);
            }
            finally
            {
                RestoreRecapClock();
                presenter.Restore();
            }
        }

        private void SetRecapClock(bool frozen)
        {
            if (frozen) PauseRecapClock();
            else RestoreRecapClock();
        }

        private void PauseRecapClock()
        {
            recapTimeScale ??= Time.timeScale;
            Time.timeScale = 0f;
        }

        private void RestoreRecapClock()
        {
            if (!recapTimeScale.HasValue) return;
            Time.timeScale = recapTimeScale.Value;
            recapTimeScale = null;
        }

        private void OnDestroy() => RestoreRecapClock();
    }
}

using System;
using System.Collections;
using Balance;
using Cameras;
using Damage;
using Game.Runs;
using Substrate;
using Substrate.Presentation;
using Substrate.Sectors;
using Substrate.Sessions;
using Ships.Loadout;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UI.Screens;
using Ships.Registry;
using Substrate.Services.Units;
using Substrate.Services.Objectives;

namespace Game
{
    /// <summary>
    /// The interactive game's host: the scene object that wraps one <see cref="Session"/> and runs the
    /// game over it as one straight-line coroutine — compose the session, build the viewport (the
    /// observer camera) and the optional <see cref="PlayerRig"/>, then loops over runs: hangar, load the
    /// sector, play until the sector ends or the player dies, run record, death recap, unload. It owns the
    /// clock, splash, hangar, run record, recap, restart, kill refill and the one EventSystem; the session only composes,
    /// loads and unloads. It builds three child roots and hands them down: <c>Viewport</c> (observer
    /// camera), <c>UI</c> (screens, HUD) and <c>Arena</c> (the session root, at the frame offset). Presentation
    /// is read from the profile once, beside the session's own snapshot, and handed down to each step.
    /// The hangar, recap and restart stand in for Home Base, multi-sector runs and player progress;
    /// why the host grows in place: https://github.com/amindell11/astronomical-home/issues/295#issuecomment-5787867584
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

        [Header("Hangar")]
        [Tooltip("Between-run hangar screen. Null → no interactive hangar (the standing loadout is " +
                 "applied silently).")]
        [SerializeField] internal HangarScreen hangarScreenPrefab;

        [Tooltip("Modules the hangar offers per slot. Null → no hangar choices (the player flies its " +
                 "prefab-authored modules).")]
        [SerializeField] internal ItemSubset hangarOffer;

        [Header("Death Policy")]
        [Tooltip("What happens when the player ship dies. RestartSector runs the death recap and " +
                 "reloads the active sector; None does nothing.")]
        [SerializeField] private PlayerDeathBehavior deathBehavior = PlayerDeathBehavior.RestartSector;

        [Header("Kill Refill")]
        [Tooltip("Fraction of max hull restored to the player on each kill the run tally counts.")]
        [SerializeField, Range(0f, 1f)] private float killHullRestore = 0.25f;

        [Header("Death Recap")]
        [Tooltip("Seconds the death recap holds before auto-continuing; the Continue button skips " +
                 "the wait. Shown only under RestartSector with presentation on.")]
        [SerializeField, Min(0f)] private float recapHoldSeconds = 8f;

        // Test seams, set before the host activates: null → the default store path and the real source.
        internal string runRecordPath;
        internal BuildIdentitySource buildIdentity;

        private RunRecordStore runRecords;
        private DamageInfo lastKillingBlow;
        private bool playerDied;
        private bool sectorEnded;

        private UnitService unitService;
        private ObjectiveService objectiveService;

        private Session session;
        private ObserverCam observer;
        private PlayerRig rigInstance;
        private LoadingSplash splash;

        private Transform viewport;
        private Transform ui;
        private Transform arena;

        public Sector ActiveSector => session?.ActiveSector;

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
            session = new Session(sessionProfile, arena, unitService, objectiveService);
            if (presentation)
                BuildEventSystem(ui);
            if (splashPrefab && presentation)
                splash = Instantiate(splashPrefab, ui);

            SetSplashVisible(true);
            yield return null;

            yield return session.Compose();
            observer = BuildObserver(session.Units, presentation, viewport);
            if (playerRig)
            {
                // An asset gets no lifecycle, and state parked on it would outlive the session.
                rigInstance = Instantiate(playerRig, transform);
                yield return rigInstance.Build(session.Units, session.Objectives, presentation,
                    observer, ui, session.Frame, BuildDeathCallback());
                rigInstance.Tally.Killed += RefillPlayerHull;
            }

            while (true)
            {
                SetSplashVisible(false);
                if (rigInstance)
                    yield return RunHangar(rigInstance, presentation, ui);

                SetSplashVisible(true);
                playerDied = false;
                sectorEnded = false;
                yield return session.LoadSector(rigInstance ? rigInstance.Player : null, _ => sectorEnded = true);
                var playerLoadoutStatHash = rigInstance ? BeginRun() : null;
                SetSplashVisible(false);

                // Run-end signals latch, so one arriving mid-load or mid-recap never cuts that step short.
                while (!playerDied && !sectorEnded)
                    yield return null;

                if (playerDied)
                {
                    AppendRunRecord(playerLoadoutStatHash);
                    if (presentation)
                        yield return RunDeathRecap();
                }

                SetSplashVisible(true);
                yield return session.UnloadSector();
                if (rigInstance) rigInstance.Park();
            }
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
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystem.transform.SetParent(parent, false);
        }

        private void SetSplashVisible(bool visible)
        {
            if (splash) splash.SetVisible(visible);
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
                        rigInstance.Tally.End(Time.time);
                        rigInstance.Spawns.End(Time.time);
                        lastKillingBlow = killingBlow;
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

        // An unreadable build identity is treated like the store's failed write: the run goes unrecorded.
        private void AppendRunRecord(string playerLoadoutStatHash)
        {
            if (!buildIdentity(out var identity, out var failure))
            {
                Debug.LogError($"Run record skipped, build identity unreadable: {failure}", this);
                return;
            }

            var sector = sessionProfile.sectorEntry.prefab;
            runRecords.Append(RunRecord.Compose(DateTime.UtcNow, sector.name, identity,
                StatHash.OfSetting(hangarOffer, sector, killHullRestore), rigInstance.Loadout, playerLoadoutStatHash,
                rigInstance.Tally, rigInstance.Ledger, rigInstance.Spawns, lastKillingBlow));
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

            var overlay = rig.Overlay;
            if (overlay) overlay.SetVisible(false);
            SetPlayerInputEnabled(rig, false);

            var screen = Instantiate(hangarScreenPrefab, uiRoot);
            var launched = false;
            screen.Show(hangarOffer, rig.Loadout, () => launched = true);

            yield return new WaitUntil(() => launched);

            rig.ApplyLoadout();
            Destroy(screen.gameObject);

            // ApplyLoadout may rebuild the player and re-bind the HUD — refs from before it are stale.
            SetPlayerInputEnabled(rig, true);
            var activeOverlay = rig.Overlay;
            if (activeOverlay) activeOverlay.SetVisible(true);
        }

        // Fire1 shares mouse 0 with UI clicks, so the commander sleeps for the hangar screen's lifetime.
        private static void SetPlayerInputEnabled(PlayerRig rig, bool inputEnabled)
        {
            if (rig.Player && rig.Player.Commander)
                rig.Player.Commander.enabled = inputEnabled;
        }

        private IEnumerator RunDeathRecap()
        {
            var overlay = rigInstance.Overlay;
            if (overlay) overlay.SetVisible(false);
            SetPlayerInputEnabled(rigInstance, false);

            var screen = DeathRecapScreen.Create(ui);
            var dismissed = false;
            screen.Show(lastKillingBlow, rigInstance.Ledger.Rows, rigInstance.Tally, () => dismissed = true);

            var deadline = Time.unscaledTime + recapHoldSeconds;
            yield return new WaitUntil(() => dismissed || Time.unscaledTime >= deadline);

            Destroy(screen.gameObject);
            SetPlayerInputEnabled(rigInstance, true);
            if (overlay) overlay.SetVisible(true);
        }
    }
}

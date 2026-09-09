using System;
using System.Collections;
using Cameras;
using Damage;
using Game.Presentation;
using Game.Sectors;
using Game.Services;
using Game.Sessions;
using Ships;
using UI;
using UnityEngine;
using Utils;
using UI.Screens;
using Ships.Registry;
using Game.Services.Units;
using Game.Services.Objectives;
using Game.Sectors.Elements;

namespace Game.Play
{
    /// <summary>
    /// The interactive game's host: the scene object that wraps one <see cref="Session"/> and is the
    /// game's interface to it. It owns the clock (a coroutine state machine paced against the frame
    /// loop), the between-run hangar flow, the splash, the death recap, and the reset policy
    /// (sector complete / player death → restart). It also builds the viewport — the observer camera
    /// every sector and the player rig frame themselves against — and the optional
    /// <see cref="PlayerRig"/>, handing both to the session at each sector load. The session
    /// orchestrates its own compose/load/unload/teardown; the host only sequences those steps.
    /// The RL harness's <c>HarnessSessionHost</c> is the other host shape, over the harness's own
    /// composition rather than a session.
    /// </summary>
    [RequireComponent(typeof(UnitService))]
    [RequireComponent(typeof(ObjectiveService))]
    public class GameSessionHost : MonoBehaviour
    {
        /// <summary>Session policy for what happens when the persistent player ship dies.</summary>
        public enum PlayerDeathBehavior { None, RespawnInPlace, RestartSector }

        [Header("Session")]
        [SerializeField] private SessionProfile sessionProfile = new SessionProfile();

        [Tooltip("The player and its HUD. Built once at Start; persists across sector restarts. " +
                 "Null → no player (spectator).")]
        [SerializeField] private PlayerRig playerRig;

        [Header("View")]
        [Tooltip("Observer camera spawned once at session start and framed on the fleet; the player " +
                 "rig and the sector's modules are handed this instance.")]
        [SerializeField] private ObserverCam observerCamPrefab;

        [Header("Splash")]
        [Tooltip("Full-screen splash shown over the non-interactive states (boot, session compose, " +
                 "sector load). Optional; skipped when presentation is off (headless/RL).")]
        [SerializeField] private LoadingSplash splashPrefab;

        [Header("Hangar")]
        [Tooltip("Between-run hangar screen. Null → no interactive hangar (the standing loadout is " +
                 "applied silently).")]
        [SerializeField] private HangarScreen hangarScreenPrefab;

        [Tooltip("Modules the hangar offers per slot. Null → no hangar choices (the player flies its " +
                 "prefab-authored modules).")]
        [SerializeField] private LoadoutConfig loadoutCatalog;

        [Header("Death Policy")]
        [Tooltip("What happens when the player ship dies. RestartSector reloads the active sector; " +
                 "RespawnInPlace revives via playerRespawn; None does nothing.")]
        [SerializeField] private PlayerDeathBehavior deathBehavior = PlayerDeathBehavior.RestartSector;

        [Tooltip("Used when deathBehavior = RespawnInPlace.")]
        [SerializeField] private RespawnPolicy playerRespawn;

        [Header("Death Recap")]
        [Tooltip("Seconds the death recap holds before auto-continuing; the Continue button skips " +
                 "the wait. Shown only under RestartSector with presentation on.")]
        [SerializeField, Min(0f)] private float recapHoldSeconds = 8f;

        private DamageInfo lastKillingBlow;

        private UnitService unitService;
        private ObjectiveService objectiveService;

        private Session session;
        private ObserverCam observer;
        private Coroutine stateRoutine;
        public GameState CurrentState { get; private set; }

        public event Action<GameState> OnGameStateChanged;

        public Sector ActiveSector => session?.ActiveSector;

        public IGameServices Services => session?.Services;

        private void Awake()
        {
            unitService = GetComponent<UnitService>();
            objectiveService = GetComponent<ObjectiveService>();

            DontDestroyOnLoad(gameObject);

            if (splashPrefab && sessionProfile.presentation)
                Instantiate(splashPrefab, transform).Initialize(this);

            TransitionTo(GameState.Loading);
        }

        private void TransitionTo(GameState newState)
        {
            if (stateRoutine != null)
                StopCoroutine(stateRoutine);

            CurrentState = newState;
            OnGameStateChanged?.Invoke(newState);
            stateRoutine = StartCoroutine(RunState(newState));
        }

        private IEnumerator RunState(GameState state)
        {
            switch (state)
            {
                case GameState.Loading:
                    yield return HandleLoading();
                    break;
                case GameState.Start:
                    yield return HandleStart();
                    break;
                case GameState.Hangar:
                    yield return HandleHangar();
                    break;
                case GameState.LoadSector:
                    yield return HandleLoadSector();
                    break;
                case GameState.InSector:
                    yield break;
                case GameState.DeathRecap:
                    yield return HandleDeathRecap();
                    break;
                case GameState.Restart:
                    yield return HandleRestart();
                    break;
                case GameState.Exit:
                    HandleExit();
                    yield break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state));
            }
        }

        private IEnumerator HandleLoading()
        {
            session = new Session(sessionProfile, transform, unitService, objectiveService);

            yield return null;
            TransitionTo(GameState.Start);
        }

        private IEnumerator HandleStart()
        {
            yield return session.Compose();

            observer = BuildObserver(session.Services);
            if (playerRig)
                yield return playerRig.Build(session.Services, observer, session.Frame, BuildDeathCallback());

            TransitionTo(GameState.Hangar);
        }

        /// <summary>Stays callable without the state machine so the presentation gate can be driven directly.</summary>
        internal ObserverCam BuildObserver(IGameServices services)
        {
            var built = Instantiate(observerCamPrefab);

            // The authored prefab clears to the skybox; a non-presenting session must not render one.
            if (!services.PresentationEnabled)
            {
                built.Cam.clearFlags = CameraClearFlags.SolidColor;
                built.Cam.backgroundColor = Color.black;
            }

            // The camera carries authored presentation of its own (the starfield backdrop, the reverb zone).
            PresentationApplier.Apply(built.gameObject, services.PresentationEnabled);

            var registry = services.UnitService.ActiveRegistry;
            if (registry == null)
                return built;

            registry.ActiveShips.OnAdd += s => built.AddSecondarySubject(s.transform);
            registry.ActiveShips.OnRemove += s => built.RemoveSecondarySubject(s.transform);
            return built;
        }

        // Services are read from the session at death time, after composition has populated them.
        private Action<ShipId, DamageInfo> BuildDeathCallback()
        {
            switch (deathBehavior)
            {
                case PlayerDeathBehavior.RestartSector:
                    return (_, killingBlow) =>
                    {
                        lastKillingBlow = killingBlow;
                        TransitionTo(GameState.DeathRecap);
                    };
                case PlayerDeathBehavior.RespawnInPlace:
                    var policy = playerRespawn;
                    if (!policy.Enabled) return null;
                    // No live producer transform here, so the authored point resolves against the frame origin.
                    return (victim, _) => session.Services.UnitService.WaitAndRespawnShip(
                        victim, Respawn.Resolve(policy, session.Frame.Offset), 0f, policy.delay);
                case PlayerDeathBehavior.None:
                default:
                    return null;
            }
        }

        /// <summary>Between-run hangar step, run before every sector load (first launch and every restart).</summary>
        private IEnumerator HandleHangar()
        {
            if (playerRig)
                yield return RunHangar(playerRig);

            TransitionTo(GameState.LoadSector);
        }

        /// <summary>Interactive hangar flow; applies the standing loadout silently when headless (never blocks on a click) and stays callable without the state machine for tests.</summary>
        internal IEnumerator RunHangar(PlayerRig rig)
        {
            if (!rig || !rig.Player || rig.Loadout == null || !hangarScreenPrefab
                || !GameSettings.PresentationEnabled)
            {
                if (rig) rig.ApplyLoadout();
                yield break;
            }

            var overlay = rig.Overlay;
            if (overlay) overlay.SetVisible(false);
            SetPlayerInputEnabled(rig, false);

            var screen = Instantiate(hangarScreenPrefab);
            var launched = false;
            screen.Show(loadoutCatalog, rig.Loadout, () => launched = true);

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

        private IEnumerator HandleLoadSector()
        {
            yield return session.LoadSector(playerRig ? playerRig.Player : null, HandleSectorComplete);

            TransitionTo(GameState.InSector);
        }

        private void HandleSectorComplete(SectorResult result) => TransitionTo(GameState.Restart);

        /// <summary>Recap hold between death and restart; headless (no presentation/rig) falls straight through.</summary>
        private IEnumerator HandleDeathRecap()
        {
            if (!GameSettings.PresentationEnabled || !playerRig)
            {
                TransitionTo(GameState.Restart);
                yield break;
            }

            var overlay = playerRig.Overlay;
            if (overlay) overlay.SetVisible(false);
            SetPlayerInputEnabled(playerRig, false);

            var screen = DeathRecapScreen.Create();
            var dismissed = false;
            screen.Show(lastKillingBlow, playerRig.Ledger.Rows, () => dismissed = true);

            var deadline = Time.unscaledTime + recapHoldSeconds;
            yield return new WaitUntil(() => dismissed || Time.unscaledTime >= deadline);

            Destroy(screen.gameObject);
            SetPlayerInputEnabled(playerRig, true);
            if (overlay) overlay.SetVisible(true);
            TransitionTo(GameState.Restart);
        }

        private IEnumerator HandleRestart()
        {
            yield return session.UnloadSector();

            TransitionTo(GameState.Hangar);
        }

        private void HandleExit()
        {
            StartCoroutine(ExitRoutine());
        }

        // Ships die before cameras: ClearAll destroys the player the rig and observer reference.
        private IEnumerator ExitRoutine()
        {
            yield return session.Teardown();
            if (playerRig)
                playerRig.Teardown();
            if (observer)
                Destroy(observer.gameObject);
            observer = null;
            session = null;
        }
    }
}

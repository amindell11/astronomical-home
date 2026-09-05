using System;
using System.Collections;
using Damage;
using Game.Sectors;
using Game.Services;
using Game.Sessions;
using Player;
using Ships;
using UI;
using UnityEngine;
using Utils;

namespace Game.Play
{
    /// <summary>
    /// The interactive game's host: the scene object that wraps one <see cref="Session"/> and is the
    /// game's interface to it. It owns the clock (a coroutine state machine paced against the frame
    /// loop), the between-run hangar flow, the splash, the death recap, and the reset policy
    /// (sector complete / player death → restart), injected into the session as its two hooks. The
    /// session orchestrates its own compose/load/unload/teardown; the host only sequences those steps.
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

        [Tooltip("Session-tier player/camera/UI/world rig. Built once at Start; persists across sector restarts.")]
        [SerializeField] private SessionRig playerRig;

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
            session = new Session(sessionProfile, transform, unitService, objectiveService, playerRig,
                HandleSectorComplete, BuildDeathCallback());

            yield return null;
            TransitionTo(GameState.Start);
        }

        private IEnumerator HandleStart()
        {
            yield return session.Compose();

            TransitionTo(GameState.Hangar);
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
                        victim,
                        Respawn.Resolve(policy, session.Services, session.Frame.Offset, session.Frame.Offset),
                        0f, policy.delay);
                case PlayerDeathBehavior.None:
                default:
                    return null;
            }
        }

        /// <summary>Between-run hangar step, run before every sector load (first launch and every restart).</summary>
        private IEnumerator HandleHangar()
        {
            if (session.Rig)
                yield return RunHangar(session.Rig, session.Services);

            TransitionTo(GameState.LoadSector);
        }

        /// <summary>Interactive hangar flow; applies the standing loadout silently when headless (never blocks on a click) and stays callable without the state machine for tests.</summary>
        internal IEnumerator RunHangar(SessionRig rig, IGameServices services)
        {
            if (!rig || !rig.Player || rig.Loadout == null || !hangarScreenPrefab
                || !GameSettings.PresentationEnabled)
            {
                if (rig) rig.ApplyLoadout();
                yield break;
            }

            var overlay = services.UIService.ActiveOverlay;
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
            var activeOverlay = services.UIService.ActiveOverlay;
            if (activeOverlay) activeOverlay.SetVisible(true);
        }

        // Fire1 shares mouse 0 with UI clicks, so the commander sleeps for the hangar screen's lifetime.
        private static void SetPlayerInputEnabled(SessionRig rig, bool inputEnabled)
        {
            if (rig.Player && rig.Player.Commander)
                rig.Player.Commander.enabled = inputEnabled;
        }

        private IEnumerator HandleLoadSector()
        {
            yield return session.LoadSector();

            TransitionTo(GameState.InSector);
        }

        private void HandleSectorComplete(SectorResult result) => TransitionTo(GameState.Restart);

        /// <summary>Recap hold between death and restart; headless (no presentation/rig) falls straight through.</summary>
        private IEnumerator HandleDeathRecap()
        {
            var rig = session.Rig;
            if (!GameSettings.PresentationEnabled || !rig)
            {
                TransitionTo(GameState.Restart);
                yield break;
            }

            var overlay = session.Services.UIService.ActiveOverlay;
            if (overlay) overlay.SetVisible(false);
            SetPlayerInputEnabled(rig, false);

            var screen = DeathRecapScreen.Create();
            var dismissed = false;
            screen.Show(lastKillingBlow, rig.Ledger.Rows, () => dismissed = true);

            var deadline = Time.unscaledTime + recapHoldSeconds;
            yield return new WaitUntil(() => dismissed || Time.unscaledTime >= deadline);

            Destroy(screen.gameObject);
            SetPlayerInputEnabled(rig, true);
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

        private IEnumerator ExitRoutine()
        {
            yield return session.Teardown();
            session = null;
        }
    }
}

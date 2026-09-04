using System;
using System.Collections;
using Damage;
using Game.Sectors;
using Game.Services;
using Player;
using Ships;
using UI;
using UnityEngine;
using Utils;
using Ships.Registry;
using Game.Sectors.Elements;

namespace Game.Session
{
    /// <summary>
    /// The interactive gameplay driver — the above-seam half of the session tier. It owns the clock
    /// (a coroutine state machine paced against the frame loop), the between-run hangar flow, and the
    /// gameplay reset policy (sector complete / player death → restart), wired onto the session via
    /// <see cref="GameSession.OnSectorComplete"/> and <see cref="GameSession.OnPlayerDeath"/>. It holds
    /// the one <see cref="GameSession"/> it drives and reads <see cref="ActiveSector"/>/<see cref="Services"/>
    /// off it.
    ///
    /// The driver-agnostic lifecycle primitives it sequences live below the seam on a sibling
    /// <see cref="SessionHost"/> (behind <see cref="ISessionPrimitives"/>); the driver never composes or
    /// tears down directly — it calls the host.
    /// </summary>
    [RequireComponent(typeof(SessionHost))]
    public class GameDriver : MonoBehaviour
    {
        /// <summary>Session policy for what happens when the persistent player ship dies.</summary>
        public enum PlayerDeathBehavior { None, RespawnInPlace, RestartSector }

        [Header("Session")]
        [SerializeField] private SessionProfile sessionProfile = new SessionProfile();

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

        private SessionHost host;

        private GameSession session;
        private Coroutine stateRoutine;
        public GameState CurrentState { get; private set; }

        public event Action<GameState> OnGameStateChanged;

        public Sector ActiveSector => session?.ActiveSector;

        public IGameServices Services => session?.Services;

        private void Awake()
        {
            host = GetComponent<SessionHost>();

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
            session = new GameSession { Profile = sessionProfile };

            yield return null;
            TransitionTo(GameState.Start);
        }

        private IEnumerator HandleStart()
        {
            // Reset-policy hooks must be set BEFORE composing so a spawn-frame death already has a subscriber.
            session.OnSectorComplete = HandleSectorComplete;
            session.OnPlayerDeath = BuildDeathCallback(session);

            yield return host.ComposeSession(session);

            TransitionTo(GameState.Hangar);
        }

        // Services are read from the session at death time, after composition has populated them.
        private Action<ShipId, DamageInfo> BuildDeathCallback(GameSession target)
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
                    // No live producer transform here, so the authored point resolves against the world origin.
                    return (victim, _) => target.Services.UnitService.WaitAndRespawnShip(
                        victim,
                        Respawn.Resolve(policy, target.Services, target.World.Offset, target.World.Offset),
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
                yield return RunHangar(session);

            TransitionTo(GameState.LoadSector);
        }

        /// <summary>Interactive hangar flow; applies the standing loadout silently when headless (never blocks on a click) and stays callable without the state machine for tests.</summary>
        internal IEnumerator RunHangar(GameSession target)
        {
            var rig = target.Rig;
            if (!rig || !rig.Player || rig.Loadout == null || !hangarScreenPrefab
                || !GameSettings.PresentationEnabled)
            {
                if (rig) rig.ApplyLoadout();
                yield break;
            }

            var overlay = target.Services.UIService.ActiveOverlay;
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
            var activeOverlay = target.Services.UIService.ActiveOverlay;
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
            yield return host.LoadSector(session);

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
            yield return host.UnloadSector(session);

            TransitionTo(GameState.Hangar);
        }

        private void HandleExit()
        {
            StartCoroutine(ExitRoutine());
        }

        private IEnumerator ExitRoutine()
        {
            yield return host.TeardownSession(session);
            session = null;
        }
    }
}

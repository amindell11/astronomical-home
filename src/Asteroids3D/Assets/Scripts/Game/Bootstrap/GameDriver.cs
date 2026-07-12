using System;
using System.Collections;
using Game.Sectors;
using Game.Services;
using Player;
using Ships;
using UI;
using UnityEngine;
using Utils;

namespace Game.Bootstrap
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
    /// tears down directly — it calls the host. GamePlane ownership and its reset-on-teardown/destroy
    /// policy likewise live on the host. This driver is the swappable component: a headless/RL driver
    /// replaces it on the same GameObject, driving the same host from its own step loop with its own
    /// policy.
    /// </summary>
    [RequireComponent(typeof(SessionHost))]
    public class GameDriver : MonoBehaviour
    {
        /// <summary>
        /// Session policy for what happens when the persistent player ship dies.
        /// <see cref="None"/> = nothing, <see cref="RespawnInPlace"/> = revive via
        /// <see cref="playerRespawn"/>, <see cref="RestartSector"/> = tear down and reload the active
        /// sector (the rig persists).
        /// </summary>
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

        private SessionHost host;

        private GameSession session;
        private Coroutine stateRoutine;
        public GameState CurrentState { get; private set; }

        public event Action<GameState> OnGameStateChanged;

        /// <summary>The active sector manager, if any.</summary>
        public Sector ActiveSector => session?.ActiveSector;

        /// <summary>The service container for this game session.</summary>
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
            // The driver sets its reset-policy hooks on the session BEFORE composing; the host's
            // primitives consume them (LoadSector wires OnSectorComplete to the sector; ComposeSession
            // passes OnPlayerDeath to the rig, which wires it onto the player synchronously at spawn, so
            // a spawn-frame death already has a subscriber — no pre-compose subscription dance).
            session.OnSectorComplete = HandleSectorComplete;
            session.OnPlayerDeath = BuildDeathCallback(session);

            yield return host.ComposeSession(session);

            TransitionTo(GameState.Hangar);
        }

        // Map the gameplay death policy to the callback the rig wires onto the player. Null for None
        // (and for a disabled RespawnInPlace policy, matching Respawn.Wire's Enabled guard); a restart
        // request for RestartSector; a producer-relative revive for RespawnInPlace. Services are read
        // from the session at death time, which is after composition has populated them.
        private Action<ShipId, ShipId> BuildDeathCallback(GameSession target)
        {
            switch (deathBehavior)
            {
                case PlayerDeathBehavior.RestartSector:
                    return (_, _) => HandleRestartRequested();
                case PlayerDeathBehavior.RespawnInPlace:
                    var policy = playerRespawn;
                    if (!policy.Enabled) return null;
                    return (victim, _) => target.Services.UnitService.WaitAndRespawnShip(
                        victim, Respawn.Resolve(policy, target.Services), 0f, policy.delay);
                case PlayerDeathBehavior.None:
                default:
                    return null;
            }
        }

        /// <summary>
        /// Between-run hangar step: show the hangar, let the player pick a loadout, and install it onto
        /// the persistent ship before the sector loads. Runs before every sector — the first launch and
        /// every restart. Runs the interactive screen when there is a player and presentation is on;
        /// otherwise it applies the standing selection silently (headless/RL never blocks here).
        /// </summary>
        private IEnumerator HandleHangar()
        {
            if (session.Rig)
                yield return RunHangar(session);

            TransitionTo(GameState.LoadSector);
        }

        /// <summary>
        /// Run the between-run hangar: show the screen, wait for the player to Launch, then install the
        /// chosen loadout via the rig. Skipped (standing selection applied silently) when there is no
        /// player, no screen, or presentation is off (headless/RL) — so an automated run never blocks
        /// on a click. Kept callable in isolation so the flow is testable without the state machine.
        /// The loadout apply is a direct rig call (not routed through the host), so this flow needs no
        /// SessionHost sibling.
        /// </summary>
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

        // Fire1 shares mouse 0 with UI clicks, so a hangar button press would fire the ship's
        // primary weapon; the commander sleeps for the screen's lifetime instead.
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

        private void HandleRestartRequested() => TransitionTo(GameState.Restart);

        private IEnumerator HandleRestart()
        {
            // GamePlane is host-owned and persists across restart (UnloadSector never resets it), so no
            // reconfigure guard is needed here.
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

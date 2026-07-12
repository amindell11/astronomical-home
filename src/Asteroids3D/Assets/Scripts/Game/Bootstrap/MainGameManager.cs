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
    /// Session-tier orchestrator, split into two layers. The <b>lifecycle primitives</b>
    /// (<see cref="ComposeSession"/>, <see cref="LoadSector"/>, <see cref="UnloadSector"/>,
    /// <see cref="TeardownSession"/>) are driver-agnostic coroutines over an explicit per-session
    /// <see cref="GameSession"/> container; they carry no reset policy. Composition configures the
    /// universal world plane (<see cref="GamePlane"/>). The <b>interactive gameplay driver</b> — the
    /// coroutine state machine below them — paces the primitives against the frame loop, owns the
    /// between-run hangar flow, and wires the gameplay reset policy (sector
    /// complete / player death → restart) via <see cref="GameSession.OnSectorComplete"/> and
    /// <see cref="GameSession.OnPlayerDeath"/>. A headless/RL harness drives the same primitives from
    /// its own step loop with its own policy. Composition inputs (sector entry, rig, policy bools)
    /// still live as serialized fields on this manager; multi-arena work would move the per-session
    /// ones into per-call/session data.
    /// </summary>
    [RequireComponent(typeof(ObjectiveService))]
    [RequireComponent(typeof(UnitService))]
    public class MainGameManager : MonoBehaviour
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

        [Header("Session Rig")]
        [Tooltip("Session-tier player/camera/UI/world rig. Built once at Start; persists across sector restarts.")]
        [SerializeField] private SessionRig playerRig;

        [Header("Game Plane")]
        [SerializeField] private PlaneAxis planeAxis = PlaneAxis.Y;
        [SerializeField] private Vector3 planeOrigin;

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

        // Sibling MonoBehaviour services ([RequireComponent]); cached once in Awake — never
        // looked up mid-lifecycle.
        private UnitService unitService;
        private ObjectiveService objectiveService;

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
            unitService = GetComponent<UnitService>();
            objectiveService = GetComponent<ObjectiveService>();

            DontDestroyOnLoad(gameObject);

            if (splashPrefab && sessionProfile.presentation)
                Instantiate(splashPrefab, transform).Initialize(this);

            TransitionTo(GameState.Loading);
        }

        // ------------------------------------------------------------------
        // Lifecycle primitives — driver-agnostic, per-session, policy-free.
        // ------------------------------------------------------------------

        /// <summary>
        /// Compose a session: service container, optional player/camera/UI rig, presentation
        /// overlay. Built once per session; the rig persists across sector loads and is removed
        /// only by <see cref="TeardownSession"/>.
        /// </summary>
        internal IEnumerator ComposeSession(GameSession target)
        {
            // Game-tier VFX policy: gate the still-un-migrated weapon/projectile/asteroid effects.
            // Runtime-only override so a headless/RL session never leaks into the saved preference.
            GameSettings.SetVfxEnabled(target.Profile.vfx);

            // Presentation policy: when off (headless/RL), each ship's embedded visual rig
            // self-disables in its Awake. Runtime-only override, same lifetime as the VFX toggle.
            GameSettings.SetPresentationEnabled(target.Profile.presentation);

            GamePlane.Configure(planeAxis, planeOrigin);

            target.Services = new GameServices(
                unitService: unitService,
                environmentService: new EnvironmentService(),
                objectiveService: objectiveService,
                cameraService: new CameraService(),
                uiService: new UIService()
            );

            if (playerRig)
            {
                target.Rig = playerRig;
                // Consume the driver-set hook (see HandleStart); never overwrite it — a headless/RL
                // driver supplies its own OnPlayerDeath before composing.
                yield return playerRig.Build(target.Services, target.Profile.buildPlayer, target.OnPlayerDeath);
            }
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
        /// Load the profile's sector into the session and subscribe the session's
        /// <see cref="GameSession.OnSectorComplete"/> policy hook to it.
        /// </summary>
        internal IEnumerator LoadSector(GameSession target)
        {
            var entry = target.Profile.sectorEntry;
            if (!entry?.prefab)
                throw new InvalidOperationException("No sector entry configured on MainGameManager.");

            // Instantiate the sector subtree under an inactive holder so authored content children
            // do not Awake until services exist and adoption has wired each object. Setup runs
            // while inert; then reparent out (world pose preserved) and drop the holder so children
            // Awake post-wiring. Authoring stays WYSIWYG — only runtime instantiation is gated.
            var holder = new GameObject("SectorLoad") { hideFlags = HideFlags.HideAndDontSave };
            holder.SetActive(false);

            var sector = Instantiate(entry.prefab, holder.transform);
            target.ActiveSector = sector;
            // Inject the persistent rig's player — the sector references it, never builds/owns it.
            sector.Initialize(target.Services, entry.config, target.Rig ? target.Rig.Player : null);

            if (target.OnSectorComplete != null)
                sector.OnSectorComplete += target.OnSectorComplete;

            // Entry reset: place the persistent player at the sector's declared start (plane-space,
            // producer-relative to the sector so it's deterministic every load). The sector only
            // DECLARES the start via PlayerStart; the session tier does the reset.
            if (target.Rig && target.Rig.Player)
                target.Services.UnitService.RespawnShip(
                    target.Rig.Player.Id, sector.PlayerStart, 0f);

            yield return sector.Setup();

            sector.transform.SetParent(null, true);
            Destroy(holder);
        }

        /// <summary>
        /// Unload the session's sector: cancel pending revives, run the sector's teardown phase,
        /// destroy its content. The session rig and service registries persist — only sector
        /// content cycles. Pair with <see cref="LoadSector"/> for a full episode reset.
        /// </summary>
        internal IEnumerator UnloadSector(GameSession target)
        {
            // Drop any queued player/NPC revives so a pending respawn can't fire into the torn-down sector.
            target.Services.UnitService.CancelPendingRespawns();

            yield return DestroyActiveSector(target, runTeardown: true);
        }

        /// <summary>
        /// Session exit: drop the sector (without running its teardown phase), then tear down the
        /// persistent rig and wipe every registry.
        /// </summary>
        internal IEnumerator TeardownSession(GameSession target)
        {
            yield return DestroyActiveSector(target, runTeardown: false);

            if (target.Rig)
                target.Rig.Teardown();
            target.Rig = null;

            target.Services?.ClearAll();
            target.Services = null;
        }

        private IEnumerator DestroyActiveSector(GameSession target, bool runTeardown)
        {
            var sector = target.ActiveSector;
            if (sector)
            {
                if (target.OnSectorComplete != null)
                    sector.OnSectorComplete -= target.OnSectorComplete;

                if (runTeardown)
                    yield return sector.Teardown();

                Destroy(sector.gameObject);
                target.ActiveSector = null;
            }
        }

        // ------------------------------------------------------------------
        // Interactive gameplay driver — the coroutine state machine.
        // Owns the clock (frame loop), the process-global GamePlane, and the gameplay reset
        // policy: sector completion and player death both restart the sector.
        // ------------------------------------------------------------------

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
            // The driver sets its reset-policy hooks on the session BEFORE composing; the primitives
            // consume them (LoadSector wires OnSectorComplete to the sector; ComposeSession passes
            // OnPlayerDeath to the rig, which wires it onto the player synchronously at spawn, so a
            // spawn-frame death already has a subscriber — no pre-compose subscription dance).
            session.OnSectorComplete = HandleSectorComplete;
            session.OnPlayerDeath = BuildDeathCallback(session);

            yield return ComposeSession(session);

            TransitionTo(GameState.Hangar);
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
            yield return LoadSector(session);

            TransitionTo(GameState.InSector);
        }

        private void HandleSectorComplete(SectorResult result) => TransitionTo(GameState.Restart);

        private void HandleRestartRequested() => TransitionTo(GameState.Restart);

        private IEnumerator HandleRestart()
        {
            yield return UnloadSector(session);

            // GamePlane persists across restarts (like the rig/services); only reconfigure if
            // something actually cleared it — reconfiguring unconditionally throws.
            if (!GamePlane.IsConfigured) GamePlane.Configure(planeAxis, planeOrigin);
            TransitionTo(GameState.Hangar);
        }

        private void HandleExit()
        {
            StartCoroutine(ExitRoutine());
        }

        private IEnumerator ExitRoutine()
        {
            yield return TeardownSession(session);
            session = null;

            GamePlane.Reset();
        }

        private void OnDestroy()
        {
            GamePlane.Reset();
        }
    }
}

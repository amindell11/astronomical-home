using System;
using System.Collections;
using Game.Sectors;
using Game.Services;
using Player;
using Presentation;
using Ships;
using UnityEngine;

namespace Game.Bootstrap
{
    /// <summary>
    /// Session-tier orchestrator, split into two layers. The <b>lifecycle primitives</b>
    /// (<see cref="ComposeSession"/>, <see cref="LoadSector"/>, <see cref="UnloadSector"/>,
    /// <see cref="TeardownSession"/>) are driver-agnostic coroutines over an explicit per-session
    /// <see cref="GameSession"/> container; they carry no reset policy and never touch the
    /// process-global <see cref="GamePlane"/>. The <b>interactive gameplay driver</b> — the
    /// coroutine state machine below them — paces the primitives against the frame loop, owns
    /// GamePlane, and wires the gameplay reset policy (sector complete / player death → restart)
    /// via <see cref="GameSession.OnSectorComplete"/> and <see cref="PlayerRig.RestartRequested"/>.
    /// A headless/RL harness drives the same primitives from its own step loop with its own policy.
    /// Composition inputs (sector entry, rig, policy bools) still live as serialized fields on this
    /// manager; multi-arena work would move the per-session ones into per-call/session data.
    /// </summary>
    [RequireComponent(typeof(ObjectiveService))]
    [RequireComponent(typeof(UnitService))]
    public class MainGameManager : MonoBehaviour
    {
        [Header("Sector")]
        [SerializeField] private SectorEntry currentSector;

        [Header("Session Rig")]
        [Tooltip("Session-tier player/camera/UI/world rig. Built once at Start; persists across sector restarts.")]
        [SerializeField] private PlayerRig playerRig;

        [Tooltip("Session policy: when false, no player ship is built (spectator/headless).")]
        [SerializeField] private bool buildPlayer = true;

        [Tooltip("Session policy: when false, ships spawn without visual rigs (headless/RL). " +
                 "Presentation is a game-tier overlay attached to each ship via the unit registry.")]
        [SerializeField] private bool installPresentation = true;

        [Header("Game Plane")]
        [SerializeField] private PlaneAxis planeAxis = PlaneAxis.Y;
        [SerializeField] private Vector3 planeOrigin;

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
                yield return playerRig.Build(target.Services, buildPlayer);
            }

            // Game-tier presentation: attach a visual rig to each active ship (player built above, plus
            // any spawned/adopted by sectors) via the unit registry. Skipped entirely for headless/RL.
            if (installPresentation)
            {
                target.Presentation = new PresentationInstaller();
                target.Presentation.Install(target.Services.UnitService);
            }
        }

        /// <summary>
        /// Load <paramref name="entry"/>'s sector into the session and subscribe the session's
        /// <see cref="GameSession.OnSectorComplete"/> policy hook to it.
        /// </summary>
        internal IEnumerator LoadSector(GameSession target, SectorEntry entry)
        {
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

            target.Presentation?.Uninstall();
            target.Presentation = null;

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
            GamePlane.Configure(planeAxis, planeOrigin);
            session = new GameSession();

            yield return null;
            TransitionTo(GameState.Start);
        }

        private IEnumerator HandleStart()
        {
            yield return ComposeSession(session);

            session.OnSectorComplete = HandleSectorComplete;
            if (session.Rig)
                session.Rig.RestartRequested += HandleRestartRequested;

            TransitionTo(GameState.LoadSector);
        }

        private IEnumerator HandleLoadSector()
        {
            yield return LoadSector(session, currentSector);

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
            TransitionTo(GameState.LoadSector);
        }

        private void HandleExit()
        {
            StartCoroutine(ExitRoutine());
        }

        private IEnumerator ExitRoutine()
        {
            if (session.Rig)
                session.Rig.RestartRequested -= HandleRestartRequested;

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

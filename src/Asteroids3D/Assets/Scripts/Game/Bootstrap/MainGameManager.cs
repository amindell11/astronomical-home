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
    /// Session-tier orchestrator, split into two layers:
    /// driver-agnostic <b>lifecycle primitives</b> (<see cref="ComposeSession"/>,
    /// <see cref="LoadSector"/>, <see cref="ResetSector"/>, <see cref="TeardownSession"/>) that
    /// operate on an explicit per-session <see cref="GameSession"/> container, and the
    /// <b>interactive gameplay driver</b> — the coroutine state machine — that paces those
    /// primitives against the frame loop and wires the in-game restart triggers. A headless/RL
    /// harness drives the same primitives from its own step loop without inheriting the driver's
    /// restart-on-complete policy.
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
        // Lifecycle primitives — driver-agnostic, per-session.
        //
        // Each primitive takes the GameSession container explicitly rather than assuming a
        // process-wide singleton, so N in-process arenas stay an additive change. GamePlane
        // configuration is the driver's job (it is process-global state, not per-session);
        // physics isolation for co-located arenas is a follow-on.
        // ------------------------------------------------------------------

        /// <summary>
        /// Compose a session: service container, optional player/camera/UI rig, presentation
        /// overlay. The rig persists across sector loads; only session teardown removes it.
        /// Carries no reset policy — what happens on player death is the driver's concern
        /// (subscribe to <see cref="PlayerRig.RestartRequested"/>), not part of composition.
        /// </summary>
        internal IEnumerator ComposeSession(GameSession target)
        {
            ComposeServices(target);
            yield return ComposeRigAndPresentation(target);
        }

        private void ComposeServices(GameSession target)
        {
            target.Services = new GameServices(
                unitService: unitService,
                environmentService: new EnvironmentService(),
                objectiveService: objectiveService,
                cameraService: new CameraService(),
                uiService: new UIService()
            );
        }

        // Build the session-tier rig (player, observer camera, UI overlay, world) exactly once,
        // before the first sector loads. It persists across sector restarts and is torn down only on
        // session exit — the sector references the player, it does not own it.
        private IEnumerator ComposeRigAndPresentation(GameSession target)
        {
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
        /// Load the configured sector into the session. <paramref name="onSectorComplete"/> is the
        /// caller's policy hook — the gameplay driver wires restart, an RL driver wires its
        /// terminal condition (or nothing).
        /// </summary>
        internal IEnumerator LoadSector(GameSession target, Action<SectorResult> onSectorComplete)
        {
            if (!currentSector?.prefab)
                throw new InvalidOperationException("No sector entry configured on MainGameManager.");

            // Instantiate the sector subtree under an inactive holder so authored content children
            // do not Awake until services exist and adoption has wired each object. Setup runs
            // while inert; then reparent out (world pose preserved) and drop the holder so children
            // Awake post-wiring. Authoring stays WYSIWYG — only runtime instantiation is gated.
            var holder = new GameObject("SectorLoad") { hideFlags = HideFlags.HideAndDontSave };
            holder.SetActive(false);

            var sector = Instantiate(currentSector.prefab, holder.transform);
            target.ActiveSector = sector;
            // Inject the persistent rig's player — the sector references it, never builds/owns it.
            sector.Initialize(target.Services, currentSector.config, target.Rig ? target.Rig.Player : null);

            target.SectorCompleteHandler = onSectorComplete;
            if (onSectorComplete != null)
                sector.OnSectorComplete += onSectorComplete;

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
        /// Reset the session's sector: tear down the loaded one and load fresh. The session rig
        /// (player/camera/UI/world) and service registries persist — only sector content cycles.
        /// </summary>
        internal IEnumerator ResetSector(GameSession target, Action<SectorResult> onSectorComplete)
        {
            yield return UnloadSector(target);
            yield return LoadSector(target, onSectorComplete);
        }

        // Teardown-only half of a reset: cancel pending revives, run sector teardown, destroy content.
        private IEnumerator UnloadSector(GameSession target)
        {
            // Drop any queued player/NPC revives so a pending respawn can't fire into the torn-down sector.
            target.Services.UnitService.CancelPendingRespawns();

            yield return DestroyActiveSector(target, runTeardown: true);

            // GamePlane is session-global and persists across restarts (like the rig/services); only
            // (re)configure if something actually cleared it. Reconfiguring unconditionally throws,
            // since the driver already configured it and nothing resets it on the restart path.
            if (!GamePlane.IsConfigured) GamePlane.Configure(planeAxis, planeOrigin);
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

            GamePlane.Reset();
        }

        private IEnumerator DestroyActiveSector(GameSession target, bool runTeardown)
        {
            var sector = target.ActiveSector;
            if (sector)
            {
                if (target.SectorCompleteHandler != null)
                    sector.OnSectorComplete -= target.SectorCompleteHandler;
                target.SectorCompleteHandler = null;

                if (runTeardown)
                    yield return sector.Teardown();

                Destroy(sector.gameObject);
                target.ActiveSector = null;
            }
        }

        // ------------------------------------------------------------------
        // Interactive gameplay driver — the coroutine state machine.
        //
        // Owns the clock (frame loop) and the reset policy (sector complete / player death →
        // restart). Everything it does goes through the primitives above.
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
            ComposeServices(session);

            yield return null;
            TransitionTo(GameState.Start);
        }

        private IEnumerator HandleStart()
        {
            yield return ComposeRigAndPresentation(session);

            // Gameplay reset policy: the rig's death→restart request is wired HERE, by the driver —
            // it is not part of session composition, so an RL driver composing the same session
            // never inherits it.
            if (playerRig)
                playerRig.RestartRequested += RequestSectorRestart;

            TransitionTo(GameState.LoadSector);
        }

        private IEnumerator HandleLoadSector()
        {
            yield return LoadSector(session, HandleSectorComplete);

            TransitionTo(GameState.InSector);
        }

        // Single gameplay reset hook: every "episode/sector ended" trigger — sector completion and
        // the rig's player-death restart request — funnels here. An RL driver wires its own terminal
        // condition to the primitives instead of inheriting this policy.
        private void RequestSectorRestart()
        {
            TransitionTo(GameState.Restart);
        }

        private void HandleSectorComplete(SectorResult result)
        {
            RequestSectorRestart();
        }

        // Restart tears down only the sector's content; the session rig (player/camera/UI/world) and
        // the service registries persist, so the player survives a restart instead of being rebuilt.
        private IEnumerator HandleRestart()
        {
            yield return UnloadSector(session);
            TransitionTo(GameState.LoadSector);
        }

        private void HandleExit()
        {
            StartCoroutine(ExitRoutine());
        }

        private IEnumerator ExitRoutine()
        {
            if (playerRig)
                playerRig.RestartRequested -= RequestSectorRestart;

            yield return TeardownSession(session);
            session = null;
        }

        private void OnDestroy()
        {
            GamePlane.Reset();
        }
    }
}

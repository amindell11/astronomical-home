using System;
using System.Collections;
using Game.Sectors;
using Game.Services;
using Ships;
using UnityEngine;

namespace Game.Bootstrap
{
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

        [Header("Game Plane")]
        [SerializeField] private PlaneAxis planeAxis = PlaneAxis.Y;
        [SerializeField] private Vector3 planeOrigin;

        private GameServices services;
        private Coroutine stateRoutine;
        public GameState CurrentState { get; private set; }

        public event Action<GameState> OnGameStateChanged;
        
        /// <summary>The active sector manager, if any.</summary>
        public Sector ActiveSector { get; private set; }

        /// <summary>The service container for this game session.</summary>
        public IGameServices Services => services;
        
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
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

            services = new GameServices(
                unitService: GetComponent<UnitService>(),
                environmentService: new EnvironmentService(),
                objectiveService: GetComponent<ObjectiveService>(),
                cameraService: new CameraService(),
                uiService: new UIService()
            );

            yield return null;
            TransitionTo(GameState.Start);
        }

        // Build the session-tier rig (player, observer camera, UI overlay, world) exactly once,
        // before the first sector loads. It persists across sector restarts and is torn down only on
        // session exit — the sector references the player, it does not own it.
        private IEnumerator HandleStart()
        {
            if (playerRig)
                yield return playerRig.Build(services, buildPlayer, () => TransitionTo(GameState.Restart));
            TransitionTo(GameState.LoadSector);
        }

        private IEnumerator HandleLoadSector()
        {
            if (!currentSector?.prefab)
                throw new InvalidOperationException("No sector entry configured on MainGameManager.");

            // Instantiate the sector subtree under an inactive holder so authored content children
            // do not Awake until services exist and adoption has wired each object. Setup runs
            // while inert; then reparent out (world pose preserved) and drop the holder so children
            // Awake post-wiring. Authoring stays WYSIWYG — only runtime instantiation is gated.
            var holder = new GameObject("SectorLoad") { hideFlags = HideFlags.HideAndDontSave };
            holder.SetActive(false);

            ActiveSector = Instantiate(currentSector.prefab, holder.transform);
            // Inject the persistent rig's player — the sector references it, never builds/owns it.
            ActiveSector.Initialize(services, currentSector.config, playerRig ? playerRig.Player : null);
            ActiveSector.OnSectorComplete += HandleSectorComplete;

            // Entry reset: place the persistent player at the sector's declared start (plane-space).
            // The sector only DECLARES the start via PlayerStart; the session tier does the reset.
            if (playerRig && playerRig.Player)
                services.UnitService.RespawnShip(
                    playerRig.Player.Id, ActiveSector.PlayerStart ?? Vector2.zero, 0f);

            yield return ActiveSector.Setup();

            ActiveSector.transform.SetParent(null, true);
            Destroy(holder);

            TransitionTo(GameState.InSector);
        }

        private void HandleSectorComplete(SectorResult result)
        {
            TransitionTo(GameState.Restart);
        }

        // Restart tears down only the sector's content; the session rig (player/camera/UI/world) and
        // the service registries persist, so the player survives a restart instead of being rebuilt.
        private IEnumerator HandleRestart()
        {
            // Drop any queued player/NPC revives so a pending respawn can't fire into the torn-down sector.
            services.UnitService.CancelPendingRespawns();

            yield return TeardownActiveSector(runTeardown: true);
            // GamePlane is session-global and persists across restarts (like the rig/services); only
            // (re)configure if something actually cleared it. Reconfiguring unconditionally throws,
            // since HandleLoading already configured it and nothing resets it on the restart path.
            if (!GamePlane.IsConfigured) GamePlane.Configure(planeAxis, planeOrigin);
            TransitionTo(GameState.LoadSector);
        }

        private void HandleExit()
        {
            StartCoroutine(ExitRoutine());
        }

        // Session exit: drop the sector, then tear down the persistent rig and wipe every registry.
        private IEnumerator ExitRoutine()
        {
            yield return TeardownActiveSector(runTeardown: false);

            if (playerRig)
                playerRig.Teardown();

            services?.ClearAll();
            services = null;

            GamePlane.Reset();
        }

        private IEnumerator TeardownActiveSector(bool runTeardown)
        {
            if (ActiveSector)
            {
                ActiveSector.OnSectorComplete -= HandleSectorComplete;

                if (runTeardown)
                    yield return ActiveSector.Teardown();

                Destroy(ActiveSector.gameObject);
                ActiveSector = null;
            }
        }

        private void OnDestroy()
        {
            GamePlane.Reset();
        }
    }
}

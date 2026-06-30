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
                    TransitionTo(GameState.LoadSector);
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
            ActiveSector.Initialize(services, currentSector.config);
            ActiveSector.OnSectorComplete += HandleSectorComplete;

            yield return ActiveSector.Setup();

            ActiveSector.transform.SetParent(null, true);
            Destroy(holder);

            TransitionTo(GameState.InSector);
        }

        private void HandleSectorComplete(SectorResult result)
        {
            TransitionTo(GameState.Restart);
        }

        private IEnumerator HandleRestart()
        {
            yield return Cleanup(runTeardown: true);
            GamePlane.Configure(planeAxis, planeOrigin);
            TransitionTo(GameState.LoadSector);
        }

        private void HandleExit()
        {
            StartCoroutine(Cleanup(runTeardown: false));
        }

        private IEnumerator Cleanup(bool runTeardown)
        {
            if (ActiveSector)
            {
                ActiveSector.OnSectorComplete -= HandleSectorComplete;

                if (runTeardown)
                    yield return ActiveSector.Teardown();

                Destroy(ActiveSector.gameObject);
                ActiveSector = null;
            }

            if (runTeardown)
                services?.ClearAll();
            else
                services = null;

            GamePlane.Reset();
        }

        private void OnDestroy()
        {
            GamePlane.Reset();
        }
    }
}

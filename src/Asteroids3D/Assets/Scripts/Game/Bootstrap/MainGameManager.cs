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
        public SectorManager ActiveSectorManager { get; private set; }

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
            if (!currentSector?.managerPrefab)
                throw new InvalidOperationException("No sector entry configured on MainGameManager.");

            ActiveSectorManager = Instantiate(currentSector.managerPrefab);
            ActiveSectorManager.Initialize(services, currentSector.config);
            ActiveSectorManager.OnSectorComplete += HandleSectorComplete;

            yield return ActiveSectorManager.Setup();

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
            if (ActiveSectorManager)
            {
                ActiveSectorManager.OnSectorComplete -= HandleSectorComplete;

                if (runTeardown)
                    yield return ActiveSectorManager.Teardown();

                Destroy(ActiveSectorManager.gameObject);
                ActiveSectorManager = null;
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

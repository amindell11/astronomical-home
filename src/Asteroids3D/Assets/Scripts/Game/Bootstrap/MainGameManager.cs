using System;
using System.Collections;
using Game.Sectors;
using Game.Services;
using Ships;
using UnityEngine;

namespace Game.Bootstrap
{
    public class MainGameManager : MonoBehaviour
    {
        [Header("Sector")]
        [SerializeField] private SectorEntry currentSector;

        [Header("Scene Refs")]
        [SerializeField] private Transform referencePlane;
        [SerializeField] private ShipRespawnRunner respawnRunner;

        private GameServices services;
        private SectorManager activeSectorManager;
        private GameState currentState;
        private Coroutine stateRoutine;

        public GameState CurrentState => currentState;
        public event Action<GameState> OnGameStateChanged;
        public event Action<Ship, Camera> PresentationReady;

        /// <summary>The active sector manager, if any.</summary>
        public SectorManager ActiveSectorManager => activeSectorManager;

        /// <summary>The service container for this game session.</summary>
        public IGameServices Services => services;

        /// <summary>Scene-owned respawn runner.</summary>
        public ShipRespawnRunner RespawnRunner => respawnRunner;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            TransitionTo(GameState.Loading);
        }

        private void TransitionTo(GameState newState)
        {
            if (stateRoutine != null)
                StopCoroutine(stateRoutine);

            currentState = newState;
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
            GamePlane.SetReferencePlane(referencePlane);

            services = new GameServices(
                unitService: new UnitService(),
                environmentService: new EnvironmentService(),
                objectiveService: new ObjectiveService(),
                cameraService: new CameraService()
            );

            yield return null;
            TransitionTo(GameState.Start);
        }

        private IEnumerator HandleLoadSector()
        {
            if (currentSector?.managerPrefab == null)
                throw new InvalidOperationException("No sector entry configured on MainGameManager.");

            activeSectorManager = Instantiate(currentSector.managerPrefab);
            activeSectorManager.Initialize(services, currentSector.config, respawnRunner);
            activeSectorManager.OnSectorComplete += HandleSectorComplete;

            yield return activeSectorManager.Setup();

            PublishPresentationReady();
            TransitionTo(GameState.InSector);
        }

        private void HandleSectorComplete(SectorResult result)
        {
            TransitionTo(GameState.Restart);
        }

        private IEnumerator HandleRestart()
        {
            yield return Cleanup(runTeardown: true);
            GamePlane.SetReferencePlane(referencePlane);
            TransitionTo(GameState.LoadSector);
        }

        private void HandleExit()
        {
            StartCoroutine(Cleanup(runTeardown: false));
        }

        private IEnumerator Cleanup(bool runTeardown)
        {
            if (respawnRunner)
                respawnRunner.ResetRunner();

            if (activeSectorManager != null)
            {
                activeSectorManager.OnSectorComplete -= HandleSectorComplete;

                if (runTeardown)
                    yield return activeSectorManager.Teardown();

                Destroy(activeSectorManager.gameObject);
                activeSectorManager = null;
            }

            if (runTeardown)
                services?.ClearAll();
            else
                services = null;

            GamePlane.Reset();
        }

        private void PublishPresentationReady()
        {
            var presentationShip = activeSectorManager?.PresentationShip;
            if (presentationShip == null)
                return;

            var uiCamera = services?.CameraService?.UICamera;
            if (uiCamera == null)
                return;

            PresentationReady?.Invoke(presentationShip, uiCamera);
        }

        private void OnDestroy()
        {
            GamePlane.Reset();
        }
    }
}

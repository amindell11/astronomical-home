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
                    // Title screen placeholder — immediately proceed for MVP
                    TransitionTo(GameState.LoadSector);
                    break;
                case GameState.LoadSector:
                    yield return HandleLoadSector();
                    break;
                case GameState.InSector:
                    // Sector manager drives gameplay; we just wait
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

            // Create services — using stubs until agent-2's implementations are merged
            services = new GameServices(
                unitService: null,       // TODO: Wave 2 wires real UnitService
                environmentService: null,
                objectiveService: null,
                cameraService: null
            );

            yield return null;
            TransitionTo(GameState.Start);
        }

        private IEnumerator HandleLoadSector()
        {
            if (currentSector?.managerPrefab == null)
                throw new InvalidOperationException("No sector entry configured on MainGameManager.");

            activeSectorManager = Instantiate(currentSector.managerPrefab);
            activeSectorManager.Initialize(services, currentSector.config);
            activeSectorManager.OnSectorComplete += HandleSectorComplete;

            yield return activeSectorManager.Setup();
            TransitionTo(GameState.InSector);
        }

        private void HandleSectorComplete(SectorResult result)
        {
            // MVP: both extracted and failed restart the same sector
            TransitionTo(GameState.Restart);
        }

        private IEnumerator HandleRestart()
        {
            if (activeSectorManager != null)
            {
                activeSectorManager.OnSectorComplete -= HandleSectorComplete;
                yield return activeSectorManager.Teardown();
                Destroy(activeSectorManager.gameObject);
                activeSectorManager = null;
            }

            services?.ClearAll();
            GamePlane.Reset();
            GamePlane.SetReferencePlane(referencePlane);

            TransitionTo(GameState.LoadSector);
        }

        private void HandleExit()
        {
            if (activeSectorManager != null)
            {
                activeSectorManager.OnSectorComplete -= HandleSectorComplete;
                Destroy(activeSectorManager.gameObject);
                activeSectorManager = null;
            }

            services = null;
            GamePlane.Reset();
        }

        private void OnDestroy()
        {
            HandleExit();
        }
    }
}

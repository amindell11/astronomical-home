using System;
using System.Collections;
using Diagnostics.Performance;
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

        [Header("Scene Refs")]
        [SerializeField] private Transform referencePlane;

        private GameServices services;
        private Coroutine stateRoutine;
        private LatencyProfilingRunner profilingRunner;
        private float profilingDeadline;
        public GameState CurrentState { get; private set; }

        public event Action<GameState> OnGameStateChanged;

        /// <summary>The active sector manager, if any.</summary>
        public SectorManager ActiveSectorManager { get; private set; }

        /// <summary>The service container for this game session.</summary>
        public IGameServices Services => services;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            if (LatencyProfilingScenario.EnsureInitializedFromCommandLine())
            {
                var settings = LatencyProfilingScenario.ActiveSettings;
                Debug.Log($"[LatencyProfiling] Detected profiling args — scenario='{settings.Scenario}'");
                profilingRunner = new LatencyProfilingRunner(settings);
                profilingDeadline = Time.realtimeSinceStartup + settings.StartupTimeoutSeconds;
            }

            TransitionTo(GameState.Loading);
        }

        private void Update()
        {
            if (profilingRunner == null)
                return;

            // If we haven't reached InSector before the deadline, fail-fast
            if (CurrentState != GameState.InSector && Time.realtimeSinceStartup > profilingDeadline)
            {
                profilingRunner.FailAndQuit(
                    $"Timed out waiting for InSector. CurrentState={CurrentState}, " +
                    $"sector={(ActiveSectorManager ? ActiveSectorManager.name : "null")}");
                profilingRunner = null;
            }
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
                    if (profilingRunner != null)
                    {
                        var runner = profilingRunner;
                        profilingRunner = null; // prevent re-entry
                        yield return runner.RunSession(gameObject);
                    }
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
            LatencyProfilingScenario.TryConfigureSector(ActiveSectorManager);
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
            GamePlane.SetReferencePlane(referencePlane);
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

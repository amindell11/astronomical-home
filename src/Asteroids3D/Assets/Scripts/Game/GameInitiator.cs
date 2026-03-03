using System;
using System.Collections;
using System.Linq;
using AI;
using Cameras;
using Game.Session;
using Player;
using Ships;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utils;
using World;
using ShipFactory = Ships.Factory;

namespace Game
{
    public class GameInitiator : MonoBehaviour, ISectorSessionOrchestrator
    {
        [SerializeField] private GameConfig gameConfig;
        [SerializeField] private Transform referencePlane;
        [SerializeField] private ShipRespawnRunner respawnRunner;

        private SessionContext sessionContext;
        private Coroutine sessionRoutine;
        private bool isInitialized;
        private bool worldSceneLoadedBySession;

        public event Action<Ship, Camera> PresentationReady;

        public ShipRegistry ShipRegistry => sessionContext?.ShipRegistry;
        public bool IsSessionActive => isInitialized;

        private Transform WorldFollowerTransform => sessionContext?.WorldFollowerTransform;

        private void Awake()
        {
            ValidateSerializedDependencies();

            if (!gameConfig)
                throw new ArgumentNullException(nameof(gameConfig));

            StartSession(SectorSessionConfig.FromGameConfig(gameConfig));
        }

        public Coroutine StartSession(SectorSessionConfig config)
        {
            if (sessionRoutine != null || isInitialized)
                return sessionRoutine;

            sessionRoutine = StartCoroutine(StartSessionRoutine(config));
            return sessionRoutine;
        }

        public Coroutine RestartSession(SectorSessionConfig config = null)
        {
            var nextConfig = config ?? sessionContext?.Config ?? (gameConfig ? SectorSessionConfig.FromGameConfig(gameConfig) : null);
            if (nextConfig == null)
                throw new ArgumentNullException(nameof(config));

            StopSession();
            return StartSession(nextConfig);
        }

        public IEnumerator Initialize(GameConfig config)
        {
            if (sessionRoutine != null || isInitialized)
                yield break;

            sessionRoutine = StartCoroutine(StartSessionRoutine(SectorSessionConfig.FromGameConfig(config)));
            yield return sessionRoutine;
        }

        public void StopSession()
        {
            if (sessionRoutine != null)
            {
                StopCoroutine(sessionRoutine);
                sessionRoutine = null;
            }

            UnbindShipRegistry();
            ClearLegacyBridge();

            if (respawnRunner)
                respawnRunner.ResetRunner();

            DestroySessionObjects();

            sessionContext?.ShipRegistry?.Dispose();
            GamePlane.Reset();

            sessionContext = null;
            isInitialized = false;
        }

        public void Shutdown()
        {
            StopSession();
        }

        private IEnumerator StartSessionRoutine(SectorSessionConfig config)
        {
            if (isInitialized)
                yield break;

            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (!config.GameConfig)
                throw new ArgumentNullException(nameof(config.GameConfig));

            ValidateSerializedDependencies();

            var startupSucceeded = false;
            try
            {
                gameConfig = config.GameConfig;
                sessionContext = new SessionContext(config, respawnRunner);
                isInitialized = true;

                yield return LoadEnvironment(config);
                BuildRuntimeServices(config);
                SpawnActors(config);
                BindPresentation(config.GameConfig);
                BindLegacyBridge();
                StartSessionFlow();

                startupSucceeded = true;
            }
            finally
            {
                if (!startupSucceeded)
                {
                    sessionRoutine = null;
                    TeardownFailedStartup();
                }
                else
                {
                    sessionRoutine = null;
                }
            }
        }

        private void TeardownFailedStartup()
        {
            isInitialized = false;

            UnbindShipRegistry();
            ClearLegacyBridge();

            if (respawnRunner)
                respawnRunner.ResetRunner();

            DestroySessionObjects();

            sessionContext?.ShipRegistry?.Dispose();
            GamePlane.Reset();

            sessionContext = null;
        }

        private IEnumerator LoadEnvironment(SectorSessionConfig config)
        {
            GamePlane.SetReferencePlane(referencePlane);

            if (config.LoadWorldScene)
            {
                yield return StartCoroutine(LoadWorldScene(config.WorldSceneName));
            }
            else
            {
                referencePlane.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
            }
        }

        private IEnumerator LoadWorldScene(string worldSceneName)
        {
            if (string.IsNullOrWhiteSpace(worldSceneName))
                yield break;

            if (!SceneManager.GetSceneByName(worldSceneName).isLoaded)
            {
                var loadOp = SceneManager.LoadSceneAsync(worldSceneName, LoadSceneMode.Additive);
                if (loadOp == null)
                    throw new InvalidOperationException($"Failed to start async load for scene '{worldSceneName}'. Verify the scene exists and is added to Build Settings.");

                while (!loadOp.isDone)
                    yield return null;

                worldSceneLoadedBySession = true;
            }

            referencePlane.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
        }

        private void BuildRuntimeServices(SectorSessionConfig config)
        {
            InitializeWorld(config.GameConfig);
            sessionContext.ShipRegistry = new ShipRegistry(config.GameConfig);
        }

        private void SpawnActors(SectorSessionConfig config)
        {
            var shipRegistry = sessionContext.ShipRegistry;

            sessionContext.Player = ShipFactory.CreateShip(
                config.GameConfig.PlayerTemplate,
                config.GameConfig.PlayerCommander,
                config.GameConfig.ShipSettings,
                0,
                config.PlayerSpawnPosition,
                config.PlayerSpawnRotation,
                postInitialize: WireShipDependencies);
            sessionContext.Player.tag = TagNames.Player;

            shipRegistry.ActiveShips.Add(sessionContext.Player);

            if (config.SpawnEnemy && config.GameConfig.EnemyTemplate)
            {
                var enemySpawn = config.GetEnemySpawnPosition();
                sessionContext.Enemy = ShipFactory.CreateShip(
                    config.GameConfig.EnemyTemplate,
                    config.GameConfig.EnemyCommander,
                    config.GameConfig.ShipSettings,
                    1,
                    enemySpawn,
                    Quaternion.identity,
                    WireShipDependencies);

                shipRegistry.ActiveShips.Add(sessionContext.Enemy);
            }

            respawnRunner.Initialize(config.GameConfig.ShipSpawnerSettings, shipRegistry, () => WorldFollowerTransform);

            if (sessionContext.World?.Follower && sessionContext.Player)
                sessionContext.World.Follower.SetTarget(sessionContext.Player.transform);
        }

        private void BindPresentation(GameConfig config)
        {
            InitializeCamera(config);
            InitializeAsteroidField(config);
            ConfigurePlayerInputProjection();
        }

        private void StartSessionFlow()
        {
            ValidateRuntimeWiring();
            PublishPresentationReady();
        }

        private void BindLegacyBridge()
        {
            if (sessionContext == null)
                return;

            sessionContext.Config.LegacyBridge.Bind(sessionContext);
        }

        private void ClearLegacyBridge()
        {
            if (sessionContext == null)
                return;

            sessionContext.Config.LegacyBridge.Clear(sessionContext);
        }

        private void InitializeCamera(GameConfig config)
        {
            if (!config.CameraRig)
                return;

            sessionContext.CameraRig = Instantiate(config.CameraRig);

            var cameraFollow = sessionContext.CameraRig.ObserverCam;
            if (sessionContext.Player)
                cameraFollow.SetSubject(sessionContext.Player.transform);

            if (sessionContext.ShipRegistry != null)
            {
                cameraFollow.AddSecondarySubjects(sessionContext.ShipRegistry.ActiveShips.Where(s => s != sessionContext.Player).Select(s => s.transform));
                sessionContext.ShipRegistry.ActiveShips.OnAdd += OnShipAddedToRegistry;
                sessionContext.ShipRegistry.ActiveShips.OnRemove += OnShipRemovedFromRegistry;
            }
        }

        private void OnShipAddedToRegistry(Ship ship)
        {
            if (!ship) return;
            sessionContext?.CameraRig?.ObserverCam?.AddSecondarySubject(ship.transform);
        }

        private void OnShipRemovedFromRegistry(Ship ship)
        {
            if (!ship) return;
            sessionContext?.CameraRig?.ObserverCam?.RemoveSecondarySubject(ship.transform);
        }

        private void InitializeAsteroidField(GameConfig config)
        {
            if (!config.AsteroidAsteroidField)
                return;

            var cullingBoundary = sessionContext.World ? sessionContext.World.AsteroidCullingBoundary : null;
            sessionContext.AsteroidField = Instantiate(config.AsteroidAsteroidField);
            sessionContext.AsteroidField.Initialize(cullingBoundary);
            sessionContext.AsteroidField.SetWorldAnchor(WorldFollowerTransform);

            if (sessionContext.CameraRig)
                sessionContext.AsteroidField.CurrentAnchorPos = () => GamePlane.ProjectOntoPlane(sessionContext.CameraRig.transform.position);
        }

        private void InitializeWorld(GameConfig config)
        {
            if (!config.World) return;
            sessionContext.World = Instantiate(config.World);
        }

        private void WireShipDependencies(Ship ship)
        {
            if (!ship)
                return;

            ship.Targeting?.SetRegistry(ShipRegistry);
            if (ship.Commander is AICommander aiCommander)
                aiCommander.SetRegistry(ShipRegistry);
        }

        private void ConfigurePlayerInputProjection()
        {
            if (sessionContext?.Player?.Commander is not PlayerCommander playerCommander)
                return;

            if (!sessionContext.CameraRig)
                return;

            playerCommander.SetScreenToGamePlane(pos =>
                GamePlane.ProjectOntoPlane(sessionContext.CameraRig.MainCamera.ScreenToWorldPoint(pos)) + GamePlane.Origin);
        }

        private void ValidateRuntimeWiring()
        {
            ValidateShipWiring(sessionContext.Player);
            if (sessionContext.Enemy)
                ValidateShipWiring(sessionContext.Enemy);

            if (sessionContext.Player?.Commander is PlayerCommander { HasScreenProjectorConfigured: false })
                throw new InvalidOperationException("PlayerCommander requires a configured screen-to-plane projector.");

            if (!respawnRunner || !respawnRunner.IsInitialized)
                throw new InvalidOperationException("ShipRespawnRunner must be initialized before gameplay starts.");

            if (GamePlane.Plane != referencePlane)
                throw new InvalidOperationException("GamePlane must be configured from the serialized reference plane.");
        }

        private void ValidateSerializedDependencies()
        {
            if (!referencePlane)
                throw new InvalidOperationException("GameInitiator requires a serialized reference plane Transform.");

            if (!respawnRunner)
                throw new InvalidOperationException("GameInitiator requires a scene-owned ShipRespawnRunner reference.");
        }

        private static void ValidateShipWiring(Ship ship)
        {
            if (!ship)
                throw new InvalidOperationException("Ship must be created before validation.");

            if (ship.Targeting && !ship.Targeting.HasRegistry)
                throw new InvalidOperationException($"TargetingComputer on ship '{ship.name}' is missing IShipRegistry wiring.");

            if (ship.Commander is AICommander { HasRegistryConfigured: false })
                throw new InvalidOperationException($"AICommander on ship '{ship.name}' is missing IShipRegistry wiring.");
        }

        private void DestroySessionObjects()
        {
            if (sessionContext == null)
                return;

            if (sessionContext.CameraRig)
                Destroy(sessionContext.CameraRig.gameObject);
            if (sessionContext.AsteroidField)
                Destroy(sessionContext.AsteroidField.gameObject);
            if (sessionContext.World)
                Destroy(sessionContext.World.gameObject);
            if (sessionContext.Player)
                Destroy(sessionContext.Player.gameObject);
            if (sessionContext.Enemy)
                Destroy(sessionContext.Enemy.gameObject);

            UnloadWorldScene(sessionContext.Config);
        }

        private void UnloadWorldScene(SectorSessionConfig config)
        {
            if (!worldSceneLoadedBySession)
                return;

            if (config == null || string.IsNullOrWhiteSpace(config.WorldSceneName))
                return;

            var scene = SceneManager.GetSceneByName(config.WorldSceneName);
            if (scene.isLoaded)
                SceneManager.UnloadSceneAsync(config.WorldSceneName);

            worldSceneLoadedBySession = false;
        }

        private void UnbindShipRegistry()
        {
            if (sessionContext?.ShipRegistry == null)
                return;

            sessionContext.ShipRegistry.ActiveShips.OnAdd -= OnShipAddedToRegistry;
            sessionContext.ShipRegistry.ActiveShips.OnRemove -= OnShipRemovedFromRegistry;
        }

        private void PublishPresentationReady()
        {
            if (sessionContext?.Player == null || sessionContext.CameraRig == null)
                return;
            PresentationReady?.Invoke(sessionContext.Player, sessionContext.CameraRig.UICamera);
        }

        private void OnDestroy()
        {
            StopSession();
        }
    }
}

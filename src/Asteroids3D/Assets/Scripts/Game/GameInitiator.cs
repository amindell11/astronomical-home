using System;
using System.Collections;
using System.Linq;
using AI;
using Asteroids.Fields;
using Cameras;
using Player;
using Ships;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utils;
using World;
using Random = UnityEngine.Random;
using ShipFactory = Ships.Factory;

namespace Game
{
    public class GameInitiator : MonoBehaviour
    {
        [SerializeField] private GameConfig gameConfig;
        [SerializeField] private Transform referencePlane;
        [SerializeField] private ShipRespawnRunner respawnRunner;

        private UpdatingAsteroidField asteroidField;
        private CameraRig cameraRig;
        private WorldRoot world;
        private Ship player, enemy;
        private bool isInitialized;

        public event Action<Ship, Camera> PresentationReady;

        public ShipRegistry ShipRegistry { get; private set; }
        private Transform WorldFollowerTransform => world && world.Follower ? world.Follower.transform : null;

        private void Awake()
        {
            ValidateSerializedDependencies();
            StartCoroutine(InitializeRoutine());
        }

        private IEnumerator InitializeRoutine()
        {
            if (!gameConfig)
                throw new ArgumentNullException(nameof(gameConfig));

            yield return Initialize(gameConfig);
        }

        public IEnumerator Initialize(GameConfig config)
        {
            if (isInitialized)
                yield break;
            if (!config)
                throw new ArgumentNullException(nameof(config));

            ValidateSerializedDependencies();

            gameConfig = config;
            isInitialized = true;

            GamePlane.SetReferencePlane(referencePlane);

            yield return StartCoroutine(LoadWorldScene());

            InitializeWorld(gameConfig);
            InitializeAsteroidField(gameConfig);
            InitializeShips(gameConfig);
            InitializeCamera(gameConfig);
            ConfigurePlayerInputProjection();
            ValidateRuntimeWiring();
            PublishPresentationReady();
        }

        private IEnumerator LoadWorldScene()
        {
            const string worldSceneName = "BasicWorld";

            if (!SceneManager.GetSceneByName(worldSceneName).isLoaded)
            {
                var loadOp = SceneManager.LoadSceneAsync(worldSceneName, LoadSceneMode.Additive);
                while (loadOp is not { isDone: true })
                    yield return null;
            }

            referencePlane.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
        }

        private void InitializeCamera(GameConfig config)
        {
            cameraRig = Instantiate(config.CameraRig);

            var cameraFollow = cameraRig.ObserverCam;
            cameraFollow.SetSubject(player.transform);
            cameraFollow.AddSecondarySubjects(ShipRegistry.ActiveShips.Where(s => s != player).Select(s => s.transform));
            ShipRegistry.ActiveShips.OnAdd += OnShipAddedToRegistry;
            ShipRegistry.ActiveShips.OnRemove += OnShipRemovedFromRegistry;
        }

        private void OnShipAddedToRegistry(Ship ship)
        {
            if (!ship) return;
            cameraRig?.ObserverCam?.AddSecondarySubject(ship.transform);
        }

        private void OnShipRemovedFromRegistry(Ship ship)
        {
            if (!ship) return;
            cameraRig?.ObserverCam?.RemoveSecondarySubject(ship.transform);
        }

        private void InitializeAsteroidField(GameConfig config)
        {
            var cullingBoundary = world.AsteroidCullingBoundary;
            asteroidField = Instantiate(config.AsteroidAsteroidField);
            asteroidField.Initialize(cullingBoundary);
            asteroidField.SetWorldAnchor(WorldFollowerTransform);
            asteroidField.CurrentAnchorPos = () => GamePlane.ProjectOntoPlane(cameraRig.transform.position);
        }

        private void InitializeShips(GameConfig config)
        {
            ShipRegistry = new ShipRegistry(config);

            player = ShipFactory.CreateShip(config.PlayerTemplate, config.PlayerCommander, config.ShipSettings, 0,
                Vector3.zero, Quaternion.identity, postInitialize: WireShipDependencies);
            player.tag = TagNames.Player;

            enemy = ShipFactory.CreateShip(config.EnemyTemplate, config.EnemyCommander, config.ShipSettings, 1,
                GamePlane.PlanePointToWorld(Random.insideUnitCircle * 5), Quaternion.identity, WireShipDependencies);

            ShipRegistry.ActiveShips.Add(player);
            ShipRegistry.ActiveShips.Add(enemy);

            respawnRunner.Initialize(config.ShipSpawnerSettings, ShipRegistry, () => WorldFollowerTransform);

            world.Follower.SetTarget(player.transform);
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
            if (player?.Commander is not PlayerCommander playerCommander)
                return;

            playerCommander.SetScreenToGamePlane(pos =>
                GamePlane.ProjectOntoPlane(cameraRig.MainCamera.ScreenToWorldPoint(pos)) + GamePlane.Origin);
        }

        private void ValidateRuntimeWiring()
        {
            ValidateShipWiring(player);
            ValidateShipWiring(enemy);

            if (player?.Commander is PlayerCommander { HasScreenProjectorConfigured: false })
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

        private void InitializeWorld(GameConfig config)
        {
            if (!config.World) return;
            world = Instantiate(config.World);
        }

        public void Shutdown()
        {
            if (ShipRegistry != null)
            {
                ShipRegistry.ActiveShips.OnAdd -= OnShipAddedToRegistry;
                ShipRegistry.ActiveShips.OnRemove -= OnShipRemovedFromRegistry;
            }

            if (cameraRig)
                Destroy(cameraRig.gameObject);
            if (asteroidField)
                Destroy(asteroidField.gameObject);
            if (world)
                Destroy(world.gameObject);
            if (player)
                Destroy(player.gameObject);
            if (enemy)
                Destroy(enemy.gameObject);

            if (respawnRunner)
                respawnRunner.ResetRunner();
            ShipRegistry?.Dispose();
            GamePlane.Reset();

            cameraRig = null;
            asteroidField = null;
            world = null;
            ShipRegistry = null;
            isInitialized = false;
        }

        private void PublishPresentationReady()
        {
            if (!player || !cameraRig) return;
            PresentationReady?.Invoke(player, cameraRig.UICamera);
        }

        private void OnDestroy()
        {
            if (ShipRegistry == null) return;
            ShipRegistry.ActiveShips.OnAdd -= OnShipAddedToRegistry;
            ShipRegistry.ActiveShips.OnRemove -= OnShipRemovedFromRegistry;
        }
    }
}

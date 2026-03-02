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

        private UpdatingAsteroidField asteroidField;
        private CameraRig cameraRig;
        private WorldRoot world;
        private Ship player, enemy;
        private ShipRespawnRunner respawnRunner;
        private UI.Overlay overlay;
        private bool isInitialized;

        public event Action<Ship, Camera> PresentationReady;

        public ShipRegistry ShipRegistry { get; private set; }

        private void Awake()
        {
            PresentationReady += HandlePresentationReady;
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

            gameConfig = config;
            isInitialized = true;

            yield return StartCoroutine(LoadWorldScene());

            InitializeWorld(gameConfig);
            InitializeAsteroidField(gameConfig);
            InitializeShips(gameConfig);
            InitializeCamera(gameConfig);
            ConfigurePlayerInputProjection();
            ValidateRuntimeWiring();
            PublishPresentationReady();
        }

        private static IEnumerator LoadWorldScene()
        {
            const string worldSceneName = "BasicWorld";

            if (!SceneManager.GetSceneByName(worldSceneName).isLoaded)
            {
                var loadOp = SceneManager.LoadSceneAsync(worldSceneName, LoadSceneMode.Additive);
                while (loadOp is not { isDone: true })
                    yield return null;
            }

            GamePlane.Plane.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
        }

        private void InitializeCamera(GameConfig config)
        {
            cameraRig = Instantiate(config.CameraRig);

            var cameraFollow = cameraRig.ObserverCam;
            cameraFollow.SetSubject(player.transform);
            cameraFollow.AddSecondarySubjects(ShipRegistry.ActiveShips.Where(s => s != player).Select(s => s.transform));
            ShipRegistry.ActiveShips.OnAdd += s => cameraFollow.AddSecondarySubject(s.transform);
            ShipRegistry.ActiveShips.OnRemove += s => cameraFollow.RemoveSecondarySubject(s.transform);
        }

        private void InitializeAsteroidField(GameConfig config)
        {
            var cullingBoundary = world.AsteroidCullingBoundary;
            asteroidField = Instantiate(config.AsteroidAsteroidField);
            asteroidField.Initialize(cullingBoundary);
            asteroidField.SetWorldAnchor(world && world.Follower ? world.Follower.transform : null);
            asteroidField.CurrentAnchorPos = () => GamePlane.ProjectOntoPlane(cameraRig.transform.position);
        }

        private void InitializeShips(GameConfig config)
        {
            ShipRegistry = new ShipRegistry(config);

            player = ShipFactory.CreateShip(config.PlayerTemplate, config.PlayerCommander, config.ShipSettings, 0,
                Vector3.zero, Quaternion.identity, WireShipDependencies);
            player.tag = TagNames.Player;

            enemy = ShipFactory.CreateShip(config.EnemyTemplate, config.EnemyCommander, config.ShipSettings, 1,
                GamePlane.PlanePointToWorld(Random.insideUnitCircle) * 5, Quaternion.identity, WireShipDependencies);

            ShipRegistry.ActiveShips.Add(player);
            ShipRegistry.ActiveShips.Add(enemy);

            respawnRunner = gameObject.GetComponent<ShipRespawnRunner>() ?? gameObject.AddComponent<ShipRespawnRunner>();
            respawnRunner.Initialize(config.ShipSpawnerSettings, ShipRegistry, () => world && world.Follower ? world.Follower.transform : null);

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
                GamePlane.ProjectOntoPlane(cameraRig.MainCamera.ScreenToWorldPoint(pos)));
        }

        private void ValidateRuntimeWiring()
        {
            ValidateShipWiring(player);
            ValidateShipWiring(enemy);

            if (player?.Commander is PlayerCommander pc && !pc.HasScreenProjectorConfigured)
                throw new InvalidOperationException("PlayerCommander requires a configured screen-to-plane projector.");

            if (!respawnRunner || !respawnRunner.IsInitialized)
                throw new InvalidOperationException("ShipRespawnRunner must be initialized before gameplay starts.");
        }

        private static void ValidateShipWiring(Ship ship)
        {
            if (!ship)
                throw new InvalidOperationException("Ship must be created before validation.");

            if (ship.Targeting && !ship.Targeting.HasRegistry)
                throw new InvalidOperationException($"TargetingComputer on ship '{ship.name}' is missing IShipRegistry wiring.");

            if (ship.Commander is AICommander aiCommander && !aiCommander.HasRegistryConfigured)
                throw new InvalidOperationException($"AICommander on ship '{ship.name}' is missing IShipRegistry wiring.");
        }

        private void InitializeWorld(GameConfig config)
        {
            if (!config.World) return;
            world = Instantiate(config.World);
        }

        public void Shutdown()
        {
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

            ShipRegistry?.Dispose();

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
            PresentationReady -= HandlePresentationReady;

            if (overlay)
                Destroy(overlay.gameObject);
        }

        private void HandlePresentationReady(Ship playerShip, Camera uiCamera)
        {
            if (!gameConfig || !gameConfig.UI || !playerShip)
                return;

            if (overlay)
                Destroy(overlay.gameObject);

            overlay = Instantiate(gameConfig.UI);
            overlay.SetCanvasWorldCamera(uiCamera);
            overlay.Initialize(playerShip);
        }
    }
}

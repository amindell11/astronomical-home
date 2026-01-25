using System;
using System.Collections;
using System.Linq;
using Asteroids.Fields;
using Cameras;
using Ships;
using UnityEngine;
using World;
using UnityEngine.SceneManagement;
using Utils;
using Random = UnityEngine.Random;
using ShipFactory  = Ships.Factory;
using ShipSpawner = Ships.Spawner;

namespace Game
{
    public class GameInitiator : MonoBehaviour
    {
        private UpdatingAsteroidField asteroidField;
        private CameraRig cameraRig;
        private UI.Overlay ui;
        private WorldRoot world;
        private Ship player, enemy;
        private ShipSpawner spawner;
        private bool isInitialized;

        public ShipRegistry ShipRegistry { get; private set; }

        public IEnumerator Initialize(GameConfig gameConfig)
        {
            if (isInitialized)
                yield break;
            if (!gameConfig)
                throw new ArgumentNullException(nameof(gameConfig));

            isInitialized = true;

            yield return StartCoroutine(LoadWorldScene());
            
            InitializeWorld(gameConfig);
            InitializeAsteroidField(gameConfig);
            InitializeShips(gameConfig);
            InitializeCamera(gameConfig);
            InitializeUI(gameConfig);
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

        private void InitializeUI(GameConfig gameConfig)
        {
            ui = Instantiate(gameConfig.UI);
            ui.SetCanvasWorldCamera(cameraRig.UICamera);
            ui.Initialize(player);
        }

        private void InitializeCamera(GameConfig gameConfig)
        {
            cameraRig = Instantiate(gameConfig.CameraRig);
            GameContext.Instance.SetMainCamera(cameraRig.MainCamera);

            var cameraFollow = cameraRig.ObserverCam;
            cameraFollow.SetSubject(player.transform);
            cameraFollow.AddSecondarySubjects(ShipRegistry.ActiveShips.Where(s => s != player).Select(s => s.transform));
            ShipRegistry.ActiveShips.OnAdd += s => cameraFollow.AddSecondarySubject(s.transform);
            ShipRegistry.ActiveShips.OnRemove += s => cameraFollow.RemoveSecondarySubject(s.transform);
        }

        private void InitializeAsteroidField(GameConfig gameConfig)
        {
            var cullingBoundary = world.AsteroidCullingBoundary;
            asteroidField = Instantiate(gameConfig.AsteroidAsteroidField);
            asteroidField.Initialize(cullingBoundary);
            asteroidField.CurrentAnchorPos = () => GamePlane.ProjectOntoPlane(cameraRig.transform.position);
        }

        private void InitializeShips(GameConfig gameConfig)
        {
            ShipRegistry = new ShipRegistry(gameConfig);
            GameContext.Instance.SetRegistry(ShipRegistry);
            spawner = new ShipSpawner(gameConfig.ShipSpawnerSettings, ShipRegistry.ActiveShips);

            player = ShipFactory.CreateShip(gameConfig.PlayerTemplate, gameConfig.PlayerCommander, gameConfig.ShipSettings, 0,
                Vector3.zero, Quaternion.identity);
            player.tag = TagNames.Player;

            enemy = ShipFactory.CreateShip(gameConfig.EnemyTemplate, gameConfig.EnemyCommander, gameConfig.ShipSettings, 1,
                GamePlane.PlanePointToWorld(Random.insideUnitCircle) * 5, Quaternion.identity);

            ShipRegistry.ActiveShips.Add(player);
            ShipRegistry.ActiveShips.Add(enemy);

            world.Follower.SetTarget(player.transform);
        }

        private void InitializeWorld(GameConfig gameConfig)
        {
            if (!gameConfig.World) return;
            world = Instantiate(gameConfig.World);
            GameContext.Instance.SetWorldFollow(world.Follower);
        }

        public void Shutdown()
        {
            if (ui)
                Destroy(ui.gameObject);
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

            ui = null;
            cameraRig = null;
            asteroidField = null;
            world = null;
            ShipRegistry = null;
            isInitialized = false;
        }
    }
}

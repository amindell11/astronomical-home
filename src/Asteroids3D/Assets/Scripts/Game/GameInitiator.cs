using System;
using System.Collections;
using System.Linq;
using Asteroids;
using Ships;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utils;
using Random = UnityEngine.Random;
using ShipSpawner = Ships.Spawner;
using AsteroidField = Asteroids.Fields.UpdatingField;

namespace Game
{
    public class GameInitiator : MonoBehaviour
    {
        private Ship player, enemy;
        private AsteroidField field;
        private Camera camera;
        private UI.Overlay ui;
        private WorldRoot world;
        private ShipSpawner shipSpawner;
        private readonly SubscribedSet<Ship> activeShips = new();

        public IEnumerator Initialize(GameInitiatorConfig config)
        {
            yield return StartCoroutine(LoadWorldScene());
            
            InitializeCoreSystems(config);
            InitializeField(config);
            InitializeShips(config);
            InitializeFollowers();
        }

        private IEnumerator LoadWorldScene()
        {
            var loadOp = SceneManager.LoadSceneAsync("BasicWorld", LoadSceneMode.Additive);
            while (loadOp is not { isDone: true })
                yield return null;

            world = ServiceLocator.Get<WorldRoot>();
            GamePlane.Plane.Rotate(Vector3.right, 90);
        }

        private void InitializeCoreSystems(GameInitiatorConfig config)
        {
            camera = Instantiate(config.CameraTemplate);
            ServiceLocator.Register(camera);
            
            ui = Instantiate(config.UI);
            ServiceLocator.Register(ui);
        }

        private void InitializeField(GameInitiatorConfig config)
        {
            field = Instantiate(config.AsteroidField);
            field.CurrentAnchorPos = () => GamePlane.ProjectOntoPlane(camera.transform.position);
            ServiceLocator.Register(field);
        }

        private void InitializeShips(GameInitiatorConfig config)
        {
            player = Factory.CreateShip(config.PlayerTemplate, config.PlayerCommander, config.ShipSettings, 0, Vector3.zero, Quaternion.identity);
            player.tag = TagNames.Player;
            
            enemy = Factory.CreateShip(config.EnemyTemplate, config.EnemyCommander, config.ShipSettings, 1,
                GamePlane.PlanePointToWorld(Random.insideUnitCircle) * 5, Quaternion.identity);
            
            activeShips.Add(player);
            activeShips.Add(enemy);
            
            var gameServices = new GameServices(player, enemy, activeShips);
            ServiceLocator.Register(gameServices);
            
            shipSpawner = new ShipSpawner(player, enemy);
            ServiceLocator.Register(shipSpawner);
        }

        private void InitializeFollowers()
        {
            world.Follow.target = player.transform;
        }
    }
}

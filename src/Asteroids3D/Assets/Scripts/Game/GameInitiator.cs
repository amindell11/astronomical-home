using System;
using System.Collections;
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
    public class GameInitiator : MonoSingleton<GameInitiator>
    {
        [SerializeField] private GameInitiatorConfig config;
        private Ship player, enemy;
        private AsteroidField field;
        private Camera camera;
        private GameObject world;
        private ShipSpawner shipSpawner;
        private readonly SubscribedSet<Ship> activeShips = new();

        protected override void Awake()
        {
            StartCoroutine(InitializeAfterWorldLoads());
        }


        private IEnumerator InitializeAfterWorldLoads()
        {
            var loadOp = SceneManager.LoadSceneAsync("BasicWorld", LoadSceneMode.Additive);
            while (loadOp is not { isDone: true })
                yield return null;
            
            world = GameObject.FindGameObjectWithTag(TagNames.World);
            if (!world)
                yield break;
            Instantiate(config.UI);
            GamePlane.Plane.Rotate(Vector3.right, 90);
            camera = Instantiate(config.CameraTemplate);
            field = (AsteroidField)Instantiate(config.AsteroidController);
            field.CurrentAnchorPos = () => GamePlane.ProjectOntoPlane(camera.transform.position);

            var player = Factory.CreateShip(config.PlayerTemplate, config.PlayerCommander, config.ShipSettings, 0, Vector3.zero, Quaternion.identity);
            player.tag = TagNames.Player;
            var enemy = Factory.CreateShip(config.EnemyTemplate, config.EnemyCommander, config.ShipSettings, 1,
                GamePlane.PlanePointToWorld(Random.insideUnitCircle) * 5, Quaternion.identity);
            activeShips.Add(player);
            activeShips.Add(enemy);
            world.GetComponent<WorldFollow>().target = player.transform;
            var cam = camera.GetComponent<CameraFollow>();
            cam.SetTargetSource(activeShips);
            cam.SetPlayer(player);
            shipSpawner = new ShipSpawner(player, enemy);
        }
    }
}

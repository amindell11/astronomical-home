using Asteroids;
using Asteroids.Fields;
using Cameras;
using World;
using Ships;
using Ships.Control;
using UnityEngine;
using ShipSpawner = Ships.Spawner;
using AsteroidField = Asteroids.Fields.UpdatingField;


namespace Game
{
    [CreateAssetMenu(fileName = "GameInitiatorConfig", menuName = "Game/Game Initiator Config")]
    public class Config : ScriptableObject
    {
        [SerializeField] private Ship playerTemplate;
        [SerializeField] private Ship enemyTemplate;
        [SerializeField] private AsteroidField asteroidField;
        [SerializeField] private UI.Overlay ui;
        [SerializeField] private CameraRig cameraRig;
        [SerializeField] private Settings shipSettings;
        [SerializeField] private Commander playerCommander;
        [SerializeField] private Commander enemyCommander;
        [SerializeField] private WorldRoot world;
        [SerializeField] private SpawnerSettings spawnerSettings;

        public Ship PlayerTemplate => playerTemplate;
        public Ship EnemyTemplate => enemyTemplate;
        public AsteroidField AsteroidField => asteroidField;
        public UI.Overlay UI => ui;
        public CameraRig CameraRig => cameraRig;
        public Settings ShipSettings => shipSettings;
        public Ships.Control.Commander PlayerCommander => playerCommander;
        public Ships.Control.Commander EnemyCommander => enemyCommander;
        public WorldRoot World => world;
        public SpawnerSettings SpawnerSettings => spawnerSettings;
    }
}




using Asteroids;
using Asteroids.Fields;
using Cameras;
using World;
using Ships;
using Ships.Control;
using UnityEngine;
using ShipSpawner = Ships.Spawner;


namespace Game
{
    [CreateAssetMenu(fileName = "GameInitiatorConfig", menuName = "Game/Game Initiator Config")]
    public class GameConfig : ScriptableObject
    {
        [SerializeField] private Ship playerTemplate;
        [SerializeField] private Ship enemyTemplate;
        [SerializeField] private UpdatingAsteroidField asteroidAsteroidField;
        [SerializeField] private UI.Overlay ui;
        [SerializeField] private CameraRig cameraRig;
        [SerializeField] private Settings shipSettings;
        [SerializeField] private Commander playerCommander;
        [SerializeField] private Commander enemyCommander;
        [SerializeField] private WorldRoot world;
        [SerializeField] private ShipSpawnerSettings shipSpawnerSettings;

        public Ship PlayerTemplate => playerTemplate;
        public Ship EnemyTemplate => enemyTemplate;
        public UpdatingAsteroidField AsteroidAsteroidField => asteroidAsteroidField;
        public UI.Overlay UI => ui;
        public CameraRig CameraRig => cameraRig;
        public Settings ShipSettings => shipSettings;
        public Ships.Control.Commander PlayerCommander => playerCommander;
        public Ships.Control.Commander EnemyCommander => enemyCommander;
        public WorldRoot World => world;
        public ShipSpawnerSettings ShipSpawnerSettings => shipSpawnerSettings;
    }
}




using Asteroids.Fields;
using Cameras;
using World;
using Ships;
using Ships.Command;
using UnityEngine;


namespace Game
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Game/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [SerializeField] private Ship playerTemplate;
        [SerializeField] private Ship enemyTemplate;
        [SerializeField] private UpdatingAsteroidField asteroidAsteroidField;
        [SerializeField] private CameraRig cameraRig;
        [SerializeField] private FrameSettings shipSettings;
        [SerializeField] private Commander playerCommander;
        [SerializeField] private Commander enemyCommander;
        [SerializeField] private WorldRoot world;
        [SerializeField] private ShipSpawnerSettings shipSpawnerSettings;

        public Ship PlayerTemplate => playerTemplate;
        public Ship EnemyTemplate => enemyTemplate;
        public UpdatingAsteroidField AsteroidAsteroidField => asteroidAsteroidField;
        public CameraRig CameraRig => cameraRig;
        public FrameSettings ShipSettings => shipSettings;
        public Commander PlayerCommander => playerCommander;
        public Commander EnemyCommander => enemyCommander;
        public WorldRoot World => world;
        public ShipSpawnerSettings ShipSpawnerSettings => shipSpawnerSettings;
    }
}




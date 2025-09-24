using Asteroids;
using Asteroids.Fields;
using Ships;
using Ships.Control;
using UnityEngine;
using ShipSpawner = Ships.Spawner;
using AsteroidField = Asteroids.Fields.UpdatingField;


namespace Game
{
    [CreateAssetMenu(fileName = "GameInitiatorConfig", menuName = "Game/Game Initiator Config")]
    public class GameInitiatorConfig : ScriptableObject
    {
        [SerializeField] private Ship playerTemplate;
        [SerializeField] private Ship enemyTemplate;
        [SerializeField] private UpdatingField asteroidField;
        [SerializeField] private UI.Overlay ui;
        [SerializeField] private Camera cameraTemplate;
        [SerializeField] private Settings shipSettings;
        [SerializeField] private Commander playerCommander;
        [SerializeField] private Commander enemyCommander;

        public Ship PlayerTemplate => playerTemplate;
        public Ship EnemyTemplate => enemyTemplate;
        public UpdatingField AsteroidField => asteroidField;
        public UI.Overlay UI => ui;
        public Camera CameraTemplate => cameraTemplate;
        public Settings ShipSettings => shipSettings;
        public Ships.Control.Commander PlayerCommander => playerCommander;
        public Ships.Control.Commander EnemyCommander => enemyCommander;
    }
}




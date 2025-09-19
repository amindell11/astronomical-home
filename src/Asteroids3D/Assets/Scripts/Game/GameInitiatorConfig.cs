using Asteroids;
using Asteroids.Fields;
using Ships;
using Ships.Control;
using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "GameInitiatorConfig", menuName = "Game/Game Initiator Config")]
    public class GameInitiatorConfig : ScriptableObject
    {
        [SerializeField] private Ships.Ship playerTemplate;
        [SerializeField] private Ships.Ship enemyTemplate;
        [SerializeField] private Field asteroidController;
        [SerializeField] private GameObject ui;
        [SerializeField] private Camera cameraTemplate;
        [SerializeField] private Settings shipSettings;
        [SerializeField] private Commander playerCommander;
        [SerializeField] private Commander enemyCommander;

        public Ship PlayerTemplate => playerTemplate;
        public Ship EnemyTemplate => enemyTemplate;
        public Field AsteroidController => asteroidController;
        public GameObject UI => ui;
        public Camera CameraTemplate => cameraTemplate;
        public Ships.Settings ShipSettings => shipSettings;
        public Ships.Control.Commander PlayerCommander => playerCommander;
        public Ships.Control.Commander EnemyCommander => enemyCommander;
    }
}



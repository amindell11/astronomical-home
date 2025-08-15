using System;
using Asteroid;
using Game;
using ShipMain;
using ShipMain.Control;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GameManagement
{
    public class GameInitiator : MonoBehaviour
    {
        [SerializeField] private Ship player;
        [SerializeField] private Ship enemy;
        [SerializeField] private BaseFieldManager asteroidController;
        [SerializeField] private GameObject ui;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private ShipMain.Settings shipSettings;
        [SerializeField] private Commander playerCommander;
        [SerializeField] private Commander enemyCommander;

        private GlobalSpawner spawner;


        private void Awake()
        {
            //SceneManager.LoadScene("BasicWorld", LoadSceneMode.Additive);
            //Instantiate(asteroidController);
            //Instantiate(ui);
            Instantiate(mainCamera);
            var _player = ShipFactory.CreateShip(player, playerCommander, shipSettings, 0, Vector3.zero, Quaternion.identity);
            var _enemy = ShipFactory.CreateShip(enemy, enemyCommander, shipSettings, 1,
                GamePlane.PlanePointToWorld(Random.insideUnitCircle) * 5, Quaternion.identity);
            spawner = new GlobalSpawner(_player, _enemy);
        }
    }
}
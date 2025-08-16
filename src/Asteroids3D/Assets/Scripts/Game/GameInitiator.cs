using System;
using System.Collections.Generic;
using Asteroid;
using Game;
using ShipMain;
using ShipMain.Control;
using UnityEngine;
using Utils;
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
        private readonly SubscribedSet<Ship> activeShips = new(); 

        private void Awake()
        {
            //SceneManager.LoadScene("BasicWorld", LoadSceneMode.Additive);
            //Instantiate(asteroidController);
            //Instantiate(ui);
            var cam = Instantiate(mainCamera).GetComponent<CameraFollow>();
            var _player = ShipFactory.CreateShip(player, playerCommander, shipSettings, 0, Vector3.zero, Quaternion.identity);
            var _enemy = ShipFactory.CreateShip(enemy, enemyCommander, shipSettings, 1,
                GamePlane.PlanePointToWorld(Random.insideUnitCircle) * 5, Quaternion.identity);
            activeShips.Add(_player);
            activeShips.Add(_enemy);
            cam.SetTargetSource(activeShips);
            cam.SetPlayer(_player);
            spawner = new GlobalSpawner(_player, _enemy);
        }
    }
}
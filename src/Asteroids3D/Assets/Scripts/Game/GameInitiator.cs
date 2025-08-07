using System;
using Asteroid;
using Game;
using ShipMain;
using ShipMain.Control;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

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
        private Ship _player;
        private Ship _enemy;


        private void Awake()
        {
            //SceneManager.LoadScene("BasicWorld", LoadSceneMode.Additive);
            //Instantiate(asteroidController);
            //Instantiate(ui);
            Instantiate(mainCamera);
            _player = ShipFactory.CreateShip(player, playerCommander, shipSettings, 0, Vector3.zero, Quaternion.identity);
           // _enemy = ShipFactory.CreateShip(enemy, enemyCommander, shipSettings, 1, Vector3.zero, Quaternion.identity);
        }
    }
}
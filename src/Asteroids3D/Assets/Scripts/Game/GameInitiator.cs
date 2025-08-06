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
        [SerializeField] private GameObject player;
        [SerializeField] private GameObject enemy;
        [SerializeField] private BaseFieldManager asteroidController;
        [SerializeField] private GameObject ui;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private ShipMain.Settings shipSettings;
        private Ship _player;
        private Ship _enemy;


        private void Awake()
        {
            SceneManager.LoadScene("BasicWorld", LoadSceneMode.Additive);
            Instantiate(asteroidController);
            Instantiate(ui);
            Instantiate(mainCamera);
            _player = ShipFactory.CreateShip<Player>(player, shipSettings, 0, Vector3.zero, Quaternion.identity);
            _enemy = ShipFactory.CreateShip<AI>(enemy, shipSettings, 1, Vector3.zero, Quaternion.identity);
        }
    }
}
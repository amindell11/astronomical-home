using System;
using System.Collections;
using System.Linq;
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
    public class GameInitiator : MonoBehaviour
    {
        private GameServices gameServices;
        private AsteroidField field;
        private Camera mainCamera;
        private UI.Overlay ui;
        private WorldRoot world;

        public IEnumerator Initialize(GameInitiatorConfig config)
        {
            yield return StartCoroutine(LoadWorldScene());
            
            InitializeCoreSystems(config);
            InitializeField(config);
            InitializeShips(config);
            SetWorldFollowTarget();
        }

        private IEnumerator LoadWorldScene()
        {
            var loadOp = SceneManager.LoadSceneAsync("BasicWorld", LoadSceneMode.Additive);
            while (loadOp is not { isDone: true })
                yield return null;

            world = ServiceLocator.Get<WorldRoot>();
            GamePlane.Plane.Rotate(Vector3.right, 90);
        }

        private void InitializeCoreSystems(GameInitiatorConfig config)
        {
            mainCamera = Instantiate(config.CameraTemplate);
            ServiceLocator.Register(mainCamera);
            
            ui = Instantiate(config.UI);
            ServiceLocator.Register(ui);
        }

        private void InitializeField(GameInitiatorConfig config)
        {
            field = Instantiate(config.AsteroidField);
            field.CurrentAnchorPos = () => GamePlane.ProjectOntoPlane(mainCamera.transform.position);
            ServiceLocator.Register(field);
        }

        private void InitializeShips(GameInitiatorConfig config)
        {
            gameServices = new GameServices(config);
            ServiceLocator.Register(gameServices);
        }

        private void SetWorldFollowTarget()
        {
            world.Follow.target = gameServices.Player.transform;
        }
    }
}

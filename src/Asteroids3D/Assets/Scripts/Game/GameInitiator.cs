using System;
using System.Collections;
using System.Linq;
using Asteroids;
using Asteroids.Fields;
using Cameras;
using Ships;
using UnityEngine;
using World;
using UnityEngine.SceneManagement;
using Utils;
using Random = UnityEngine.Random;
using ShipSpawner = Ships.Spawner;

namespace Game
{
    public class GameInitiator : MonoBehaviour
    {
        private UpdatingAsteroidField asteroidField;
        private CameraRig cameraRig;
        private UI.Overlay ui;
        private WorldRoot world;
        private bool isInitialized;

        public Services Services { get; private set; }

        public IEnumerator Initialize(GameConfig gameConfig)
        {
            if (isInitialized)
                yield break;
            if (!gameConfig)
                throw new ArgumentNullException(nameof(gameConfig));

            isInitialized = true;

            yield return StartCoroutine(LoadWorldScene());
            
            InitializeWorld(gameConfig);
            InitializeAsteroidField(gameConfig);
            InitializeShips(gameConfig);
            InitializeCamera(gameConfig);
            InitializeUI(gameConfig);
        }

        private IEnumerator LoadWorldScene()
        {
            const string worldSceneName = "BasicWorld";

            if (!SceneManager.GetSceneByName(worldSceneName).isLoaded)
            {
                var loadOp = SceneManager.LoadSceneAsync(worldSceneName, LoadSceneMode.Additive);
                while (loadOp is not { isDone: true })
                    yield return null;
            }

            // Ensure a deterministic reference plane orientation.
            // (Use +90° X rotation so the GamePlane's local XY maps to world XZ.)
            GamePlane.Plane.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
        }

        private void InitializeUI(GameConfig gameConfig)
        {
            ui = Instantiate(gameConfig.UI);
            ui.SetCanvasWorldCamera(cameraRig.UICamera);
        }

        private void InitializeCamera(GameConfig gameConfig)
        {
            cameraRig = Instantiate(gameConfig.CameraRig);
            var cameraFollow = cameraRig.ObserverCam;
            cameraFollow.SetSubject(Services.Player.transform);
            cameraFollow.AddSecondarySubjects(Services.ActiveShips.Where(s => s != Services.Player).Select(s => s.transform));
            Services.ActiveShips.OnAdd += s => cameraFollow.AddSecondarySubject(s.transform);
            Services.ActiveShips.OnRemove += s => cameraFollow.RemoveSecondarySubject(s.transform);
        }

        private void InitializeAsteroidField(GameConfig gameConfig)
        {
            var cullingBoundary = world.AsteroidCullingBoundary;
            asteroidField = Instantiate(gameConfig.AsteroidAsteroidField);
            asteroidField.Initialize(cullingBoundary);
            asteroidField.CurrentAnchorPos = () => GamePlane.ProjectOntoPlane(cameraRig.transform.position);
        }

        private void InitializeShips(GameConfig gameConfig)
        {
            Services = new Services(gameConfig);
            world.Follower.SetTarget(Services.Player.transform);
        }

        private void InitializeWorld(GameConfig gameConfig)
        {
            if (!gameConfig.World) return;
            world = Instantiate(gameConfig.World);
        }

        public void Shutdown()
        {
            if (ui)
                Destroy(ui.gameObject);
            if (cameraRig)
                Destroy(cameraRig.gameObject);
            if (asteroidField)
                Destroy(asteroidField.gameObject);
            if (world)
                Destroy(world.gameObject);

            Services?.Dispose();

            ui = null;
            cameraRig = null;
            asteroidField = null;
            world = null;
            Services = null;
            isInitialized = false;
        }
    }
}

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
    public class Initiator : MonoBehaviour
    {
        private AsteroidField field;
        private CameraRig cameraRig;
        private UI.Overlay ui;
        private WorldFollow world;
        private bool isInitialized;

        public Services Services { get; private set; }

        public IEnumerator Initialize(Config config)
        {
            if (isInitialized)
                yield break;
            if (!config)
                throw new ArgumentNullException(nameof(config));

            isInitialized = true;

            yield return StartCoroutine(LoadWorldScene());
            
            InitializeShips(config);
            InitializeCamera(config);
            InitializeUI(config);
            InitializeAsteroidField(config);
            InitializeWorld(config);
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

        private void InitializeUI(Config config)
        {
            ui = Instantiate(config.UI);
            ui.SetCanvasWorldCamera(cameraRig.UICamera);
        }

        private void InitializeCamera(Config config)
        {
            cameraRig = Instantiate(config.CameraRig);
            var cameraFollow = cameraRig.GetComponent<CameraFollow>();
            cameraFollow.SetSubject(Services.Player.transform);
            cameraFollow.AddSecondarySubjects(Services.ActiveShips.Where(s => s != Services.Player).Select(s => s.transform));
            Services.ActiveShips.OnAdd += s => cameraFollow.AddSecondarySubject(s.transform);
            Services.ActiveShips.OnRemove += s => cameraFollow.RemoveSecondarySubject(s.transform);
        }

        private void InitializeAsteroidField(Config config)
        {
            field = Instantiate(config.AsteroidField);
            field.CurrentAnchorPos = () => GamePlane.ProjectOntoPlane(cameraRig.transform.position);
        }

        private void InitializeShips(Config config)
        {
            Services = new Services(config);
        }

        private void InitializeWorld(Config config)
        {
            // Composition root / final wiring hook.
            // Intentionally minimal for now; keep game state transitions and teardown in GameContext.
            if (config.World)
            {
                world = Instantiate(config.World);
                world.SetTarget(Services.Player.transform);
            }
        }

        public void Shutdown()
        {
            if (ui)
                Destroy(ui.gameObject);
            if (cameraRig)
                Destroy(cameraRig.gameObject);
            if (field)
                Destroy(field.gameObject);
            if (world)
                Destroy(world.gameObject);

            Services?.Dispose();

            ui = null;
            cameraRig = null;
            field = null;
            world = null;
            Services = null;
            isInitialized = false;
        }
    }
}

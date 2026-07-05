#if UNITY_EDITOR
using Game;
using Movement.MPC;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tests.PlayMode.ChaseBenchmark
{
    /// <summary>
    /// The "watchable Testbench" entry point: drop this on a GameObject and enter Play to
    /// eyeball one live chase (pursuer MaintainRange vs evader Flee in the BigField) using the
    /// exact same scenario the headless benchmark drives. Launch it from
    /// <c>Tools ▸ Chase Benchmark ▸ Live View</c>, which builds a throwaway scene and enters
    /// Play — no committed scene asset (authoring one needs the editor open; this needs nothing).
    /// </summary>
    public sealed class ChaseBenchmarkBootstrap : MonoBehaviour
    {
        [Tooltip("Which run of the default sweep to watch. Vary startOffset/seedBias to see other layouts.")]
        public ChaseRunConfig config = ChaseRunConfig.Default(0);

        private ChaseBenchmarkScenario scenario;
        private ShipRegistry registry;

        private void Start()
        {
            GamePlane.Configure(PlaneAxis.Y);        // a bare scene has no GamePlane setup yet
            SolverBuffers.SeedBias = config.seedBias;
            registry = new ShipRegistry();
            scenario = new ChaseBenchmarkScenario(config, registry);
            Debug.Log("[ChaseBenchmark] live view: pursuer (team 0, MaintainRange) vs evader (team 1, Flee)");
        }

        private void OnDestroy()
        {
            scenario?.Dispose();
            registry?.Dispose();
            SolverBuffers.SeedBias = 0;
        }

        [MenuItem("Tools/Chase Benchmark/Live View")]
        private static void OpenLiveView()
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Top-down camera framing the XZ play plane (GamePlane = Y-up).
            if (Camera.main)
            {
                var cam = Camera.main.transform;
                cam.position = new Vector3(0f, 90f, 0f);
                cam.rotation = Quaternion.Euler(90f, 0f, 0f);
            }

            new GameObject("ChaseBenchmarkBootstrap").AddComponent<ChaseBenchmarkBootstrap>();
            EditorApplication.EnterPlaymode();
        }
    }
}
#endif

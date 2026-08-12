#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Capture;
using Game.Diagnostics;
using Game.RLHarness;
using Game.Services;
using NUnit.Framework;
using Tests.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Tests.PlayMode
{
    /// <summary>Graphics-gated proof that the capture lane films: a small capture-lane session on the smoke fixture writes PNG frames, a manifest, one clip dir per episode under the caller-named out dir, and one JSONL row per episode (no summary). Excluded from the merge gate — offscreen capture needs a real graphics device.</summary>
    [Category("AI")]
    [Category("RequiresGraphics")]
    public class RLCapturePlayModeTests
    {
        private const string GameViewCaptureType =
            "Game.Capture.GameView.GameViewEpisodeCapture, Game.Capture.GameView.Editor";
        private GameObject arenaHost;
        private UnitService unitService;
        private ArenaContext arena;
        private ProjectileService projectiles;
        private HarnessAssets assets;
        private string outDir;

        [SetUp]
        public void SetUp()
        {
            AudioListener.pause = true;
            GameSettings.SetPresentationEnabled(true);
            arenaHost = new GameObject("[CaptureArena]");
            unitService = arenaHost.AddComponent<UnitService>();
            arena = TestArena.On(arenaHost, unitService.ActiveRegistry);
            unitService.SetArena(arena);
            projectiles = new ProjectileService(arenaHost.transform);
            assets = UnityEditor.AssetDatabase.LoadAssetAtPath<HarnessAssets>(HarnessAssets.AssetPath);
            Assert.IsNotNull(assets, $"HarnessAssets missing at {HarnessAssets.AssetPath}");
            unitService.SetProjectiles(projectiles);
            PacingContract.Apply();
            Time.maximumDeltaTime = 1f;
            outDir = Path.Combine(Path.GetTempPath(), "rl-capture-test-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            projectiles?.ReturnAllToPool();
            if (arenaHost) UnityEngine.Object.DestroyImmediate(arenaHost);
            CaptureRecorder.SweepStranded();
            GameSettings.SetPresentationEnabled(true);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            AudioListener.pause = false;
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator CaptureLane_FilmsFramesManifestAndRows()
        {
            if (UnityEditor.AssetDatabase.LoadMainAssetAtPath(ShipAgentFactory.SmokeFixturePath) == null)
                Assert.Fail($"ONNX fixture missing at {ShipAgentFactory.SmokeFixturePath}");

            var spec = new SessionSpec
            {
                lane = SessionLane.Capture,
                model = UnityEditor.AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>(
                    ShipAgentFactory.SmokeFixturePath),
                seeds = new[] { EvalProtocol.HeldOutSeeds[0] },
                tag = "capture-test",
                episodesPerSeed = 2,
                fieldDensityScale = EvalProtocol.CanonicalFieldDensityScale,
                opponentKind = OpponentKind.Mirror,
                probes = new ProbeSpec[0],
                painters = new[] { DiagnosticPainters.ShipDiagnostics, DiagnosticPainters.Policy },
                outDir = outDir,
                record = new RecordPlan
                {
                    enabled = true, all = true, width = 320, height = 240, everyFixedSteps = 5,
                },
            };

            var host = NewHost(spec);
            yield return new CaptureClient().Run(host, spec);

            var jsonl = Directory.GetFiles(outDir, "*.jsonl");
            Assert.AreEqual(1, jsonl.Length, "one capture JSONL under the out dir");
            Assert.AreEqual(spec.episodesPerSeed, File.ReadAllLines(jsonl[0]).Length, "one row per filmed episode");
            Assert.IsEmpty(Directory.GetFiles(outDir, "*-summary.json"), "capture writes no summary artifact");

            var frameDirs = Directory.GetDirectories(Path.Combine(outDir, "frames"));
            Assert.AreEqual(spec.episodesPerSeed, frameDirs.Length, "one clip dir per episode, under the out dir");
            foreach (var dir in frameDirs)
            {
                Assert.IsTrue(File.Exists(Path.Combine(dir, "manifest.json")), $"{dir}: manifest present");
                Assert.Greater(Directory.GetFiles(dir, "f_*.png").Length, 0, $"{dir}: frames written");
            }
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator NativeCaptureLane_RecordsGameViewAndRestoresEditorState()
        {
            if (UnityEditor.AssetDatabase.LoadMainAssetAtPath(ShipAgentFactory.SmokeFixturePath) == null)
                Assert.Fail($"ONNX fixture missing at {ShipAgentFactory.SmokeFixturePath}");

            var priorSelection = UnityEditor.Selection.objects;
            var priorActive = UnityEditor.Selection.activeObject;
            var priorFocusedWindow = UnityEditor.EditorWindow.focusedWindow;
            var priorRunInBackground = Application.runInBackground;
            Assert.IsTrue(UnityEngine.Rendering.GraphicsSettings.TryGetCurrentRenderPipelineGlobalSettings(
                out var globalSettings), "URP global settings must be registered for native Game View capture.");
            var priorGlobalSettingsDirty = UnityEditor.EditorUtility.IsDirty(globalSettings);
            var registered = UnityEditor.GizmoUtility.GetGizmoInfo();
            Assert.IsTrue(UnityEditor.GizmoUtility.TryGetGizmoInfo(typeof(Movement.MPC.Navigator), out var priorNavigator),
                $"Navigator annotation missing; registered: {string.Join(", ", registered.Select(info => info.name))}");

            var spec = new SessionSpec
            {
                lane = SessionLane.Capture,
                model = UnityEditor.AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>(
                    ShipAgentFactory.SmokeFixturePath),
                seeds = new[] { EvalProtocol.HeldOutSeeds[0] },
                tag = "native-capture-test",
                episodesPerSeed = 1,
                fieldDensityScale = EvalProtocol.CanonicalFieldDensityScale,
                opponentKind = OpponentKind.Mirror,
                probes = Array.Empty<ProbeSpec>(),
                painters = Array.Empty<string>(),
                gizmoProfile = GizmoCaptureProfile.Steering,
                outDir = outDir,
                record = new RecordPlan
                {
                    enabled = true, all = true, width = 320, height = 240, everyFixedSteps = 5,
                },
            };

            var host = NewHost(spec, nativeCapture: true);
            yield return new CaptureClient().Run(host, spec);

            var jsonl = Directory.GetFiles(outDir, "*.jsonl");
            Assert.AreEqual(1, jsonl.Length);
            Assert.AreEqual(1, File.ReadAllLines(jsonl[0]).Length);
            var frameDirs = Directory.GetDirectories(Path.Combine(outDir, "frames"));
            Assert.AreEqual(1, frameDirs.Length);
            Assert.IsTrue(File.Exists(Path.Combine(frameDirs[0], "manifest.json")));
            Assert.Greater(Directory.GetFiles(frameDirs[0], "f_*.png").Length, 0);

            CollectionAssert.AreEqual(priorSelection, UnityEditor.Selection.objects);
            Assert.AreEqual(priorActive, UnityEditor.Selection.activeObject);
            Assert.AreEqual(priorFocusedWindow, UnityEditor.EditorWindow.focusedWindow);
            Assert.AreEqual(priorRunInBackground, Application.runInBackground);
            Assert.AreEqual(priorGlobalSettingsDirty, UnityEditor.EditorUtility.IsDirty(globalSettings));
            Assert.IsTrue(UnityEditor.GizmoUtility.TryGetGizmoInfo(typeof(Movement.MPC.Navigator), out var restored));
            Assert.AreEqual(priorNavigator.gizmoEnabled, restored.gizmoEnabled);
            Assert.AreEqual(priorNavigator.iconEnabled, restored.iconEnabled);
        }

        // Host on an inactive GameObject so its Start never fires — the test drives the client directly.
        private HarnessSessionHost NewHost(SessionSpec spec, bool nativeCapture = false)
        {
            var hostObject = new GameObject("[HarnessSessionHost]");
            hostObject.transform.SetParent(arenaHost.transform, false);
            hostObject.SetActive(false);
            var host = hostObject.AddComponent<HarnessSessionHost>();
            IEpisodeCapture capture = null;
            if (nativeCapture)
            {
                var captureType = Type.GetType(GameViewCaptureType, throwOnError: true);
                capture = (IEpisodeCapture)ScriptableObject.CreateInstance(captureType);
            }
            host.Initialize(spec, assets, unitService, arena, projectiles, capture);
            if (nativeCapture) Assert.IsTrue(host.HasEpisodeCapture, "host retained the injected native capture module");
            return host;
        }
    }
}
#endif

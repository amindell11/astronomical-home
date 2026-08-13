#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game;
using Game.Capture;
using Game.RLHarness;
using Game.Services;
using NUnit.Framework;
using Ships;
using Tests.Common;
using Tests.PlayMode.Common;
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
        private bool ownsOutDir;
        private Ship subjectA;
        private Ship subjectB;

        private const int ToggleWindowSteps = 14;
        private const string EnabledWindowA = "gizmos-on-first";
        private const string DisabledWindow = "gizmos-off";
        private const string EnabledWindowB = "gizmos-on-again";
        private const string GizmoSelector = "RL_HARNESS_GIZMOS";
        private const string PainterSelector = "RL_HARNESS_PAINTERS";

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
            ownsOutDir = true;
        }

        [TearDown]
        public void TearDown()
        {
            ShipTestFactory.DestroyShip(subjectA);
            ShipTestFactory.DestroyShip(subjectB);
            subjectA = null;
            subjectB = null;
            projectiles?.ReturnAllToPool();
            if (arenaHost) UnityEngine.Object.DestroyImmediate(arenaHost);
            CaptureRecorder.SweepStranded();
            GameSettings.SetPresentationEnabled(true);
            if (ownsOutDir && Directory.Exists(outDir)) Directory.Delete(outDir, true);
            AudioListener.pause = false;
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator CaptureLane_FilmsFramesManifestAndRows()
        {
            if (UnityEditor.AssetDatabase.LoadMainAssetAtPath(ShipAgentFactory.SmokeFixturePath) == null)
                Assert.Fail($"ONNX fixture missing at {ShipAgentFactory.SmokeFixturePath}");

            var spec = SpecFor(PainterSelector, PinnedPainterSpec);
            GameSettings.SetPresentationEnabled(spec.Presentation);

            var host = NewHost(spec);
            yield return new CaptureClient().Run(host, spec);

            Assert.AreEqual(spec.episodesPerSeed, File.ReadAllLines(EpisodeJsonlIn(spec)).Length,
                "one row per filmed episode");
            Assert.IsEmpty(Directory.GetFiles(spec.outDir, "*-summary.json"), "capture writes no summary artifact");
            AssertClipsFilmed(spec);
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

            var spec = SpecFor(GizmoSelector, PinnedNativeSpec);
            // Native profiles film collider silhouettes and gizmo geometry; presentation meshes would occlude them.
            GameSettings.SetPresentationEnabled(spec.Presentation);
            Assert.IsFalse(spec.Presentation, "a native gizmo profile films with presentation disabled");

            var host = NewHost(spec, nativeCapture: true);
            yield return new CaptureClient().Run(host, spec);

            Assert.AreEqual(spec.episodesPerSeed, File.ReadAllLines(EpisodeJsonlIn(spec)).Length);
            AssertClipsFilmed(spec);

            CollectionAssert.AreEqual(priorSelection, UnityEditor.Selection.objects);
            Assert.AreEqual(priorActive, UnityEditor.Selection.activeObject);
            Assert.AreEqual(priorFocusedWindow, UnityEditor.EditorWindow.focusedWindow);
            Assert.AreEqual(priorRunInBackground, Application.runInBackground);
            Assert.AreEqual(priorGlobalSettingsDirty, UnityEditor.EditorUtility.IsDirty(globalSettings));
            Assert.IsTrue(UnityEditor.GizmoUtility.TryGetGizmoInfo(typeof(Movement.MPC.Navigator), out var restored));
            Assert.AreEqual(priorNavigator.gizmoEnabled, restored.gizmoEnabled);
            Assert.AreEqual(priorNavigator.iconEnabled, restored.iconEnabled);
        }

        /// <summary>Pins the observed Game View effect of <c>GizmoUtility</c>: Unity documents that interface largely in Scene View terms, so the capture backend rests on an unwritten contract. Toggling the profile's component types off mid-clip must empty the frames and toggling them back on must refill them.</summary>
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator NativeCapture_GameViewFollowsGizmoTypeToggles()
        {
            GameSettings.SetPresentationEnabled(false);
            subjectA = ShipTestFactory.CreateDefaultShipAt(GamePlane.PlanePointToWorld(new Vector2(-12f, 0f)),
                GamePlane.Rotation, projectiles, team: 0);
            subjectB = ShipTestFactory.CreateDefaultShipAt(GamePlane.PlanePointToWorld(new Vector2(12f, 0f)),
                GamePlane.Rotation, projectiles, team: 1);
            Assert.IsTrue(subjectA && subjectB, "both capture subjects spawned");

            // One clip per window, so each window's frames are attributable and a one-way toggle cannot hide in an aggregate.
            yield return FilmToggleWindow(EnabledWindowA, profileEnabled: true);
            yield return FilmToggleWindow(DisabledWindow, profileEnabled: false);
            yield return FilmToggleWindow(EnabledWindowB, profileEnabled: true);

            Assert.Greater(DrawnFramesIn(EnabledWindowA), 0,
                "with the profile's types enabled the Game View renders native gizmo geometry");
            Assert.AreEqual(0, DrawnFramesIn(DisabledWindow),
                "disabling those same types empties every frame: GizmoUtility governs Game View, not only the Scene View");
            Assert.Greater(DrawnFramesIn(EnabledWindowB), 0,
                "re-enabling them refills the frames, so the Game View follows the toggle in both directions");
        }

        private IEnumerator FilmToggleWindow(string clipName, bool profileEnabled)
        {
            var capture = NewNativeCapture();
            try
            {
                // Combat, not Steering: its damage-bar drawer needs only a live ship, while steering
                // diagnostics stay empty for subjects no UnitService registered an opponent for.
                capture.Begin(ToggleConfig(clipName), GizmoCaptureProfile.Combat, subjectA, subjectB, projectiles);
                var profileAnnotations = EnabledAnnotations();
                Assert.IsNotEmpty(profileAnnotations, "Begin leaves exactly the profile's component types enabled");
                if (!profileEnabled) SetAnnotations(profileAnnotations, false);
                yield return StepFrames(capture, ToggleWindowSteps);
            }
            finally
            {
                capture.End();
                UnityEngine.Object.DestroyImmediate((UnityEngine.Object)capture);
            }
        }

        private CaptureConfig ToggleConfig(string clipName) => new()
        {
            outputRoot = outDir,
            runStamp = "toggle",
            clipName = clipName,
            width = 320,
            height = 240,
            everyFixedSteps = 2,
        };

        private int DrawnFramesIn(string clipName)
        {
            var frames = Directory.GetFiles(Path.Combine(outDir, "frames", $"toggle-{clipName}"), "f_*.png");
            Assert.IsNotEmpty(frames, $"{clipName}: the window filmed frames");
            var drawn = 0;
            foreach (var frame in frames)
                if (!IsUniform(frame))
                    drawn++;
            return drawn;
        }

        private static IEnumerator StepFrames(IEpisodeCapture capture, int steps)
        {
            for (var i = 0; i < steps; i++)
            {
                yield return new WaitForFixedUpdate();
                capture.Step();
            }
        }

        // The transaction is the only authority on which types it enabled, so read them back.
        private static List<UnityEditor.GizmoInfo> EnabledAnnotations()
        {
            var enabled = new List<UnityEditor.GizmoInfo>();
            foreach (var info in UnityEditor.GizmoUtility.GetGizmoInfo())
                if (info.gizmoEnabled)
                    enabled.Add(info);
            return enabled;
        }

        private static void SetAnnotations(List<UnityEditor.GizmoInfo> annotations, bool enabled)
        {
            foreach (var info in annotations)
            {
                var toggled = info;
                toggled.gizmoEnabled = enabled;
                UnityEditor.GizmoUtility.ApplyGizmoInfo(toggled, false);
            }
        }

        private static bool IsUniform(string pngPath)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.IsTrue(ImageConversion.LoadImage(texture, File.ReadAllBytes(pngPath)),
                    $"{pngPath} is not a readable PNG");
                var pixels = texture.GetPixels32();
                foreach (var pixel in pixels)
                    if (!pixel.Equals(pixels[0]))
                        return false;
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private IEpisodeCapture NewNativeCapture() => (IEpisodeCapture)ScriptableObject.CreateInstance(
            Type.GetType(GameViewCaptureType, throwOnError: true));

        /// <summary>With this backend's selector set in the environment the windowed run IS the production capture lane, resolving through SessionSpec exactly as a batch session would; otherwise it films the pinned spec. The selectors are mutually exclusive at the parse boundary, so each test claims the environment only when its own backend is named.</summary>
        private SessionSpec SpecFor(string backendSelector, Func<SessionSpec> pinned)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(backendSelector))) return pinned();

            var spec = SessionSpec.ParseEval(Environment.GetEnvironmentVariable,
                source => LoadModel(source == null
                    ? ShipAgentFactory.SmokeFixturePath
                    : TrainingBootstrap.ImportEvalCandidate(source)),
                source => LoadModel(TrainingBootstrap.ImportEvalOpponent(source)),
                () => SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null);
            // Only the temp out dir is ours to delete; a caller-named one is the caller's to keep.
            if (string.IsNullOrEmpty(spec.outDir)) spec.outDir = outDir;
            else ownsOutDir = false;
            outDir = spec.outDir;
            return spec;
        }

        private SessionSpec PinnedPainterSpec() => new()
        {
            lane = SessionLane.Capture,
            model = LoadModel(ShipAgentFactory.SmokeFixturePath),
            seeds = new[] { EvalProtocol.HeldOutSeeds[0] },
            tag = "capture-test",
            episodesPerSeed = 2,
            fieldDensityScale = EvalProtocol.CanonicalFieldDensityScale,
            opponentKind = OpponentKind.Mirror,
            probes = Array.Empty<ProbeSpec>(),
            painters = Array.Empty<string>(),
            outDir = outDir,
            record = new RecordPlan { enabled = true, all = true, width = 320, height = 240, everyFixedSteps = 5 },
        };

        private SessionSpec PinnedNativeSpec() => new()
        {
            lane = SessionLane.Capture,
            model = LoadModel(ShipAgentFactory.SmokeFixturePath),
            seeds = new[] { EvalProtocol.HeldOutSeeds[0] },
            tag = "native-capture-test",
            episodesPerSeed = 1,
            fieldDensityScale = EvalProtocol.CanonicalFieldDensityScale,
            opponentKind = OpponentKind.Mirror,
            probes = Array.Empty<ProbeSpec>(),
            painters = Array.Empty<string>(),
            gizmoProfile = GizmoCaptureProfile.Steering,
            outDir = outDir,
            record = new RecordPlan { enabled = true, all = true, width = 320, height = 240, everyFixedSteps = 5 },
        };

        /// <summary>The episode log, told apart from each probe's own JSONL by the spec that selected those probes.</summary>
        private static string EpisodeJsonlIn(SessionSpec spec)
        {
            var probeLogs = Array.ConvertAll(spec.probes, probe => $"-{probe.name}.jsonl");
            var episode = Directory.GetFiles(spec.outDir, "*.jsonl")
                .Where(path => !probeLogs.Any(suffix => path.EndsWith(suffix, StringComparison.Ordinal)))
                .ToArray();
            Assert.AreEqual(1, episode.Length, "exactly one episode JSONL under the caller-named out dir");
            return episode[0];
        }

        private static void AssertClipsFilmed(SessionSpec spec)
        {
            var frameDirs = Directory.GetDirectories(Path.Combine(spec.outDir, "frames"));
            Assert.AreEqual(spec.episodesPerSeed, frameDirs.Length, "one clip dir per episode, under the out dir");
            foreach (var dir in frameDirs)
            {
                var manifest = Path.Combine(dir, "manifest.json");
                Assert.IsTrue(File.Exists(manifest), $"{dir}: manifest present");
                Assert.Greater(Directory.GetFiles(dir, "f_*.png").Length, 0, $"{dir}: frames written");
                StringAssert.Contains("\"medianStepMs\"", File.ReadAllText(manifest),
                    $"{dir}: the sealed manifest carries the per-step cost the backend comparison reads");
            }
        }

        private static Unity.InferenceEngine.ModelAsset LoadModel(string assetPath) =>
            UnityEditor.AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>(assetPath);

        // Host on an inactive GameObject so its Start never fires — the test drives the client directly.
        private HarnessSessionHost NewHost(SessionSpec spec, bool nativeCapture = false)
        {
            var hostObject = new GameObject("[HarnessSessionHost]");
            hostObject.transform.SetParent(arenaHost.transform, false);
            hostObject.SetActive(false);
            var host = hostObject.AddComponent<HarnessSessionHost>();
            var capture = nativeCapture ? NewNativeCapture() : null;
            host.Initialize(spec, assets, unitService, arena, projectiles, capture);
            if (nativeCapture) Assert.IsTrue(host.HasEpisodeCapture, "host retained the injected native capture module");
            return host;
        }
    }
}
#endif

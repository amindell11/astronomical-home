#if UNITY_EDITOR
using System;
using System.IO;
using Capture;
using Capture.GameView;
using NUnit.Framework;
using Substrate;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    [Category("Camera")]
    public sealed class CapturePresetEditModeTests
    {
        [Test]
        public void Apply_SetsTheThreePresetsAndRejectsAnyOther()
        {
            var topdown = new CaptureConfig { preset = CapturePreset.GameTopdown };
            CapturePreset.Apply(topdown);
            Assert.AreEqual(CaptureFramingMode.Observer, topdown.framing);

            var lineup = new CaptureConfig { preset = CapturePreset.LineupNeutral };
            CapturePreset.Apply(lineup);
            Assert.AreEqual(CaptureFramingMode.Subjects, lineup.framing);

            var hud = new CaptureConfig { preset = CapturePreset.Hud1280x720 };
            CapturePreset.Apply(hud);
            Assert.AreEqual(1280, hud.width);
            Assert.AreEqual(720, hud.height);

            var unknown = new CaptureConfig { preset = "close-up" };
            Assert.Throws<ArgumentException>(() => CapturePreset.Apply(unknown));
        }

        [Test]
        public void ObserverFraming_MatchesTheAuthoredCameraPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cameras/Main Camera.prefab");
            Assert.IsNotNull(prefab);
            var camera = prefab.GetComponent<Cameras.ObserverCam>();
            Assert.IsNotNull(camera);
            var serialized = new SerializedObject(camera);
            Assert.AreEqual(CapturePreset.ObserverPadding, serialized.FindProperty("padding").floatValue);
            Assert.AreEqual(CapturePreset.ObserverMinZoom, serialized.FindProperty("minZoom").floatValue);
            Assert.AreEqual(CapturePreset.ObserverMaxZoom, serialized.FindProperty("maxZoom").floatValue);
            var offset = serialized.FindProperty("offset");
            Assert.AreEqual(CapturePreset.ObserverPlaneOffset.x, offset.FindPropertyRelative("x").floatValue);
            Assert.AreEqual(CapturePreset.ObserverPlaneOffset.y, offset.FindPropertyRelative("y").floatValue);
            Assert.AreEqual(CapturePreset.ObserverPlaneOffset.z, offset.FindPropertyRelative("z").floatValue);
        }

        [Test]
        public void ObserverFraming_ClampsASingleSubjectToThePrefabZoom()
        {
            var root = new GameObject("Observer framing", typeof(Camera));
            try
            {
                var camera = root.GetComponent<Camera>();
                camera.orthographic = true;
                var config = new CaptureConfig
                {
                    width = 1280,
                    height = 720,
                    framing = CaptureFramingMode.Observer,
                };
                CaptureFraming.Apply(camera, config, new[] { Vector2.zero });
                Assert.AreEqual(CapturePreset.ObserverMinZoom, camera.orthographicSize);
                var alongNormal = Vector3.Dot(camera.transform.position - GamePlane.Origin, GamePlane.Normal);
                Assert.AreEqual(CapturePreset.ObserverPlaneOffset.z, alongNormal, 1e-3f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Artifacts_WriteProvenanceAndPhaseColumns()
        {
            var root = Path.Combine(Path.GetTempPath(), "capture-preset-" + Guid.NewGuid().ToString("N"));
            try
            {
                var config = new CaptureConfig
                {
                    outputRoot = root,
                    clipName = "clip",
                    runStamp = "stamp",
                    preset = CapturePreset.Hud1280x720,
                };
                CapturePreset.Apply(config);
                var artifacts = new CaptureArtifacts(config);
                artifacts.RecordPhase("banner_alpha", 0.25f);
                artifacts.CommitFrame();
                artifacts.Complete(new CaptureCost());

                var provenance = File.ReadAllText(Path.Combine(artifacts.FrameDir, "provenance.json"));
                StringAssert.Contains("\"source\": \"hud-1280x720\"", provenance);
                StringAssert.Contains("\"size\": \"1280x720\"", provenance);
                StringAssert.Contains("\"dataKind\": \"frames\"", provenance);
                StringAssert.Contains("\"timingBasis\": \"unscaled\"", provenance);
                StringAssert.Contains("\"commit\":", provenance);

                var phases = File.ReadAllLines(Path.Combine(artifacts.FrameDir, "phases.csv"));
                Assert.AreEqual("frame,unscaled_seconds,sim_seconds,banner_alpha", phases[0]);
                StringAssert.Contains(",0.25", phases[1]);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void OverlayAndQuality_RestoreAfterAHudCaptureTouchesThem()
        {
            var state = new CaptureRecoveryState();
            CaptureEditorSettings.Snapshot(state);
            Assert.IsTrue(state.overlayFound, "play-in-editor FMOD overlay should be in the project settings");
            var level = QualitySettings.GetQualityLevel();
            try
            {
                CaptureEditorSettings.HidePlayInEditorOverlay();
                Assert.IsTrue(CaptureEditorSettings.TryReadOverlay(out var hidden, out var hasValue));
                Assert.AreEqual(0, hidden);
                Assert.IsTrue(hasValue);
                var other = level == 0 ? 1 : 0;
                QualitySettings.SetQualityLevel(other, false);
                RenderSettings.ambientLight = Color.magenta;
            }
            finally
            {
                CaptureEditorSettings.Restore(state);
            }
            Assert.AreEqual(level, QualitySettings.GetQualityLevel());
            Assert.AreEqual(state.ambientR, RenderSettings.ambientLight.r, 1e-4f);
            Assert.IsTrue(CaptureEditorSettings.TryReadOverlay(out var restored, out var restoredHas));
            Assert.AreEqual(state.overlayValue, restored);
            Assert.AreEqual(state.overlayHasValue, restoredHas);
        }
    }
}
#endif

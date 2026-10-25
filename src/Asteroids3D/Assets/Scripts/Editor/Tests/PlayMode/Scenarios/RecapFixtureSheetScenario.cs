#if UNITY_EDITOR
using Capture.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cameras;
using Capture;
using Capture.Recording;
using Game;
using Game.Preview;
using NUnit.Framework;
using Ships;
using Substrate.Sectors;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using Tests.PlayMode.Recording;
using UI.Recap;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Tests.PlayMode.Scenarios
{
    /// <summary>
    /// The recap's fixture sheet, filmed through the <see cref="PreviewHost"/> over the game's first
    /// sector: every fixture at its card, the first victory and defeat fixtures at their banner, each at
    /// two aspect ratios; a defeat is staged from its death first so the card sits over the aftermath.
    /// Ends with a screenshot of the control panel window, the only verification the panel gets.
    /// </summary>
    public sealed class RecapFixtureSheetScenario : CaptureScenario
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string ObserverCamPrefabPath = "Assets/Prefabs/Cameras/Main Camera.prefab";
        private const string SectorPrefabPath = "Assets/Prefabs/Sectors/TrialSector.prefab";
        private const string SectorConfigPath = "Assets/Settings/Game/SurvivalTrialConfig.asset";
        private const string FeelPath = "Assets/Settings/UI/RecapFeel.asset";
        private const string PanelTitle = "Preview Host";
        private static readonly (int width, int height)[] Sizes = { (1280, 720), (1680, 720) };
        private static readonly int[] BannerFixtures = { 2, 8 };
        private const int BannerHoldSpan = 2;
        private const int TabStripHeight = 22;

        private GameObject hostGo;
        private PreviewHost host;
        private string stamp;

        public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;

        public override bool NeedsSession => false;

        public override IEnumerator Run()
        {
            stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            try
            {
                BootHost();
                yield return WaitFor(() => host.Subject != null, "host composed");
                var recap = (RecapPreviewSubject)host.Subject;
                yield return new WaitForSecondsRealtime(4f);
                yield return FilmTopdown();

                for (var i = 0; i < recap.Fixtures.Count; i++)
                {
                    yield return Stage(recap, i);
                    host.Transport.Scrub(recap.SpanCount - 1, 0f);
                    for (var frame = 0; frame < 3; frame++) yield return null;
                    yield return Film($"sheet-{i:00}");
                }
                foreach (var i in BannerFixtures)
                {
                    yield return Stage(recap, i);
                    host.Transport.Scrub(BannerHoldSpan, 0f);
                    for (var frame = 0; frame < 3; frame++) yield return null;
                    yield return Film($"banner-{i:00}");
                }
                yield return ScreenshotPanel();
            }
            finally
            {
                if (hostGo) UnityEngine.Object.DestroyImmediate(hostGo);
            }
        }

        private void BootHost()
        {
            hostGo = new GameObject("SheetHost");
            hostGo.SetActive(false);
            host = hostGo.AddComponent<PreviewHost>();
            host.sessionProfile = new SessionProfile
            {
                sectors = new[]
                {
                    new SectorEntry
                    {
                        prefab = AssetDatabase.LoadAssetAtPath<Sector>(SectorPrefabPath),
                        config = AssetDatabase.LoadAssetAtPath<SectorSettings>(SectorConfigPath)
                    }
                },
                presentation = true
            };
            host.playerRigPrefab = AssetDatabase.LoadAssetAtPath<PlayerRig>(RigPrefabPath);
            host.observerCamPrefab = AssetDatabase.LoadAssetAtPath<ObserverCam>(ObserverCamPrefabPath);
            host.recapFeel = AssetDatabase.LoadAssetAtPath<RecapFeelProfile>(FeelPath);
            Assert.IsTrue(host.recapFeel, $"feel profile asset at {FeelPath}");
            hostGo.SetActive(true);
        }

        // A defeat plays its staged death through before the card, so the card sits over the aftermath.
        private IEnumerator Stage(RecapPreviewSubject recap, int fixture)
        {
            recap.SelectFixture(fixture);
            host.Transport.Scrub(0, 0f);
            for (var frame = 0; frame < 3; frame++) yield return null;
            if (recap.Context.Outcome != RecapOutcome.Defeat) yield break;
            yield return WaitFor(() => !recap.Busy, "the staged death's effects finish");
        }

        private IEnumerator Film(string still)
        {
            foreach (var (width, height) in Sizes)
            {
                using var recording = Capture.Prepare(new CaptureRequest("results/capture",
                    $"{still}-{width}x{height}", Commit, width, height, stamp));
                yield return recording.Ready;
                recording.Start();
                for (var i = 0; i < 6; i++) yield return null;
                Result = recording.Finish();
            }
        }

        private IEnumerator FilmTopdown()
        {
            var priorPresentation = CaptureViewAssertions.PresentationSnapshot();
            var priorView = CaptureViewAssertions.ViewSnapshot();
            var priorCamera = host.Observer.Cam.enabled;
            using (var view = (IDisposable)Capture.GetType().GetMethod("PrepareGameTopdown")
                       .Invoke(Capture, new object[] { host.Observer }))
            {
                using var recording = Capture.Prepare(new CaptureRequest("results/capture", "game-topdown", Commit,
                    1280, 720, stamp, new Dictionary<string, Func<double>>
                    {
                        ["observer_zoom"] = () => host.Observer.Cam.orthographicSize,
                        ["observer_x"] = () => host.Observer.transform.position.x,
                        ["observer_y"] = () => host.Observer.transform.position.y,
                    }));
                yield return recording.Ready;
                yield return new WaitForSecondsRealtime(1f);
                Assert.Less(Quaternion.Angle(Quaternion.LookRotation(Substrate.GamePlane.Normal, Substrate.GamePlane.Forward),
                    host.Observer.transform.rotation), .1f, "the actual observer owns topdown orientation");
                var subject = host.Observer.Cam.WorldToViewportPoint(host.Observer.Subject.position);
                Assert.That(subject.x, Is.InRange(0f, 1f));
                Assert.That(subject.y, Is.InRange(0f, 1f));
                recording.Start();
                for (var frame = 0; frame < 8; frame++) yield return null;
                Result = recording.Finish();
                Assert.Greater(Result.FrameCount, 1);
            }
            Assert.AreEqual(priorCamera, host.Observer.Cam.enabled);
            yield return CaptureViewAssertions.Restored(priorPresentation, priorView);
            var files = Directory.GetFiles(Result.FrameDirectory, "*.png");
            Array.Sort(files, StringComparer.Ordinal);
            Assert.AreEqual(Result.FrameCount, files.Length);
            Assert.AreEqual(Result.FrameCount + 1, File.ReadAllLines(Result.FrameMetadataPath).Length);
            StringAssert.Contains(Commit, File.ReadAllText(Result.ProvenancePath));
            var image = new Texture2D(2, 2);
            try
            {
                Assert.IsTrue(ImageConversion.LoadImage(image, File.ReadAllBytes(files[files.Length / 2])));
                Assert.AreEqual(1280, image.width);
                Assert.AreEqual(720, image.height);
                var pixels = image.GetPixels32();
                var distinct = new HashSet<Color32>();
                for (var y = image.height / 4; y < image.height * 3 / 4; y++)
                    for (var x = image.width / 4; x < image.width * 3 / 4; x++)
                        distinct.Add(pixels[y * image.width + x]);
                Assert.Greater(distinct.Count, 100, "topdown contains rendered scene content away from screen-edge overlays");
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }

        // The window itself, read off the screen: the only reflection-free way to see an editor window.
        private IEnumerator ScreenshotPanel()
        {
            EditorApplication.ExecuteMenuItem("Window/" + PanelTitle);
            var window = Resources.FindObjectsOfTypeAll<EditorWindow>().First(w => w.titleContent.text == PanelTitle);
            window.position = new Rect(120f, 120f, 440f, 560f);
            window.Focus();
            for (var i = 0; i < 6; i++) yield return null;
            var rect = window.position;
            var width = Mathf.RoundToInt(rect.width);
            var height = Mathf.RoundToInt(rect.height) + TabStripHeight;
            var pixels = InternalEditorUtility.ReadScreenPixel(new Vector2(rect.x, rect.y - TabStripHeight), width, height);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.SetPixels(pixels);
            texture.Apply();
            var path = Path.Combine(Result.OutputDirectory, $"{stamp}-panel.png");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            Debug.Log($"[Capture] control panel -> {path}");
            window.Close();
        }

        private static IEnumerator WaitFor(Func<bool> condition, string what) =>
            AsyncAssert.WaitUntil(condition, 45f, what);
    }
}
#endif

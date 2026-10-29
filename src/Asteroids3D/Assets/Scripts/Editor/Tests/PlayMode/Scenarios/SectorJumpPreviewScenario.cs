#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Capture.Recording;
using Capture.Views;
using Game;
using Game.Preview;
using NUnit.Framework;
using Substrate.Sectors;
using Substrate.Sessions;
using Tests.PlayMode.Recording;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tests.PlayMode.Scenarios
{
    public sealed class SectorJumpPreviewScenario : CaptureScenario
    {
        public override bool NeedsSession => false;
        public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;

        public override IEnumerator Run()
        {
            var root = new GameObject("Jump preview capture");
            root.SetActive(false);
            var host = root.AddComponent<PreviewHost>();
            host.sessionProfile = new SessionProfile
            {
                sectors = new[]
                {
                    new SectorEntry
                    {
                        prefab = AssetDatabase.LoadAssetAtPath<Sector>("Assets/Prefabs/Sectors/TrialSector.prefab"),
                        config = AssetDatabase.LoadAssetAtPath<SectorSettings>("Assets/Settings/Game/SurvivalTrialConfig.asset")
                    }
                },
                presentation = true
            };
            host.gameHostPrefab = AssetDatabase.LoadAssetAtPath<GameHost>("Assets/Prefabs/Game/GameHost.prefab");
            host.subjectKind = PreviewSubjectKind.SectorJump;
            var priorTimeScale = Time.timeScale;
            try
            {
                root.SetActive(true);
                yield return WaitFor(() => host.Subject != null);
                var jump = (SectorJumpPreviewSubject)host.Subject;
                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Locale_3"));
                var player = root.GetComponentInChildren<PlayerRig>().Player;
                player.transform.rotation = Quaternion.identity;
                yield return null;
                var hullCamera = root.GetComponentsInChildren<Camera>().Single(camera => camera.name == "Jump hull camera");
                var hullSize = hullCamera.orthographicSize;
                player.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
                yield return null;
                Assert.That(hullCamera.orthographicSize, Is.EqualTo(hullSize).Within(.001f),
                    "The starting heading must not change the ship's framing.");
                player.transform.rotation = Quaternion.identity;
                using (var view = (IDisposable)Capture.GetType().GetMethod("PrepareHud1280x720").Invoke(Capture, null))
                using (var recording = Capture.Prepare(new CaptureRequest("results/capture", "sector-jump-preview", Commit,
                           1280, 720, measurements: new Dictionary<string, Func<double>>
                           {
                               ["jump_seconds"] = () => jump.State.Seconds,
                               ["jump_speed"] = () => jump.State.Speed,
                               ["hud_alpha"] = () => jump.State.HudAlpha,
                               ["travel_fraction"] = () => jump.State.TravelFraction
                           })))
                {
                    yield return recording.Ready;
                    host.Transport.Restart();
                    host.Transport.Playing = false;
                    recording.Start();
                    var departure = host.Transport.Length(jump.Span(0));
                    var length = departure + jump.Span(1).Seconds;
                    var steps = Mathf.CeilToInt(length * 50f);
                    for (var frame = 0; frame <= steps; frame++)
                    {
                        var seconds = Mathf.Min(frame / 50f, length);
                        if (seconds <= departure) host.Transport.Scrub(0, seconds);
                        else host.Transport.Scrub(1, seconds - departure);
                        yield return null;
                    }
                    yield return null;
                    Result = recording.Finish();
                    Assert.That(Result.FrameCount, Is.GreaterThan(1));
                    Assert.That(jump.State.HudAlpha, Is.EqualTo(1f));
                    Assert.That(jump.State.TravelFraction, Is.EqualTo(1f));
                    AssertBackdropChanged(Result);
                }
                host.Transport.Scrub(0, 3.1f);
                yield return null;
                var expected = jump.State;
                host.Transport.Scrub(0, 1f);
                yield return null;
                host.Transport.Scrub(0, 3.1f);
                yield return null;
                Assert.That(jump.State, Is.EqualTo(expected));
                host.SelectSubject(PreviewSubjectKind.RunRecap);
                Assert.That(Time.timeScale, Is.EqualTo(priorTimeScale));
                yield return WaitFor(() => host.Subject != null);
                Assert.That(host.Subject, Is.TypeOf<RecapPreviewSubject>());
                yield return WaitFor(() => !SceneManager.GetSceneByName("Locale_1").isLoaded);
                host.Subject.SelectFixture(6);
                host.Transport.Restart();
                yield return WaitFor(() => !player.gameObject.activeInHierarchy);
                host.SelectSubject(PreviewSubjectKind.SectorJump);
                yield return WaitFor(() => host.Subject != null);
                Assert.That(player.gameObject.activeInHierarchy, Is.True,
                    "Entering jump after a staged defeat must restore its live ship.");
                yield return null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
            Assert.That(Time.timeScale, Is.EqualTo(priorTimeScale));
        }

        private static void AssertBackdropChanged(CaptureResult result)
        {
            var rows = File.ReadAllLines(result.FrameMetadataPath);
            var first = new Texture2D(2, 2);
            var last = new Texture2D(2, 2);
            try
            {
                first.LoadImage(File.ReadAllBytes(Path.Combine(result.FrameDirectory, rows[1].Split(',')[1])));
                last.LoadImage(File.ReadAllBytes(Path.Combine(result.FrameDirectory, rows[rows.Length - 1].Split(',')[1])));
                var difference = 0f;
                var samples = 0;
                for (var y = 200; y < 500; y += 10)
                for (var x = 100; x < 400; x += 10)
                {
                    var delta = first.GetPixel(x, y) - last.GetPixel(x, y);
                    difference += Mathf.Abs(delta.r) + Mathf.Abs(delta.g) + Mathf.Abs(delta.b);
                    samples++;
                }
                Assert.That(difference / samples, Is.GreaterThan(.08f),
                    "Game View must show the destination sky, not the unchanged sector behind the jump canvas.");
            }
            finally
            {
                UnityEngine.Object.Destroy(first);
                UnityEngine.Object.Destroy(last);
            }
        }

        private static IEnumerator WaitFor(Func<bool> ready)
        {
            var deadline = Time.realtimeSinceStartup + 45f;
            while (!ready())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Preview did not finish composing or playing.");
                yield return null;
            }
        }
    }
}
#endif

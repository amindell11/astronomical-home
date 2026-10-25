#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Cameras;
using Capture.Recording;
using Capture.Views;
using Game;
using Game.Preview;
using Game.Travel;
using NUnit.Framework;
using Substrate.Sectors;
using Substrate.Sessions;
using Tests.PlayMode.Recording;
using UI.Recap;
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
            host.playerRigPrefab = AssetDatabase.LoadAssetAtPath<PlayerRig>("Assets/Prefabs/MiscObjects/PlayerRig.prefab");
            host.observerCamPrefab = AssetDatabase.LoadAssetAtPath<ObserverCam>("Assets/Prefabs/Cameras/Main Camera.prefab");
            host.recapFeel = AssetDatabase.LoadAssetAtPath<RecapFeelProfile>("Assets/Settings/UI/RecapFeel.asset");
            host.jumpFeel = AssetDatabase.LoadAssetAtPath<SectorJumpProfile>("Assets/Settings/Game/SectorJumpTuning.asset");
            host.jumpShader = Shader.Find("Astronomical/Travel/SectorJump");
            host.subjectKind = PreviewSubjectKind.SectorJump;
            var priorTimeScale = Time.timeScale;
            try
            {
                root.SetActive(true);
                yield return WaitFor(() => host.Subject != null);
                var jump = (SectorJumpPreviewSubject)host.Subject;
                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Locale_3"));
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
                    host.Transport.Playing = true;
                    recording.Start();
                    yield return WaitFor(() => !host.Transport.Playing);
                    yield return null;
                    Result = recording.Finish();
                    Assert.That(Result.FrameCount, Is.GreaterThan(60));
                    Assert.That(jump.State.HudAlpha, Is.EqualTo(1f));
                    Assert.That(jump.State.TravelFraction, Is.EqualTo(1f));
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
                yield return WaitFor(() => host.Subject != null);
                Assert.That(host.Subject, Is.TypeOf<RecapPreviewSubject>());
                Assert.That(Time.timeScale, Is.EqualTo(priorTimeScale));
                yield return WaitFor(() => !SceneManager.GetSceneByName("Locale_1").isLoaded);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
            Assert.That(Time.timeScale, Is.EqualTo(priorTimeScale));
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

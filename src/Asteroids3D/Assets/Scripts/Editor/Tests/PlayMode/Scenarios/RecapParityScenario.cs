#if UNITY_EDITOR
using System;
using System.Collections;
using Cameras;
using Capture;
using Game;
using Game.Preview;
using Game.Runs;
using NUnit.Framework;
using Ships;
using Substrate;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using UI.Recap;
using UnityEditor;
using UnityEngine;

namespace Tests.PlayMode.Scenarios
{
    /// <summary>
    /// The host side of the recap's pixel-parity check: a <see cref="PreviewHost"/> over a bare sector,
    /// the observer camera pointed at empty space, filmed at the same hold states and sizes as the
    /// GameHost baseline (extracted card; victory and defeat at banner-hold and card). The
    /// contexts mirror what a real run produces when each sector is extracted at six seconds of tally.
    /// </summary>
    public sealed class RecapParityScenario : CaptureScenario
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string ObserverCamPrefabPath = "Assets/Prefabs/Cameras/Main Camera.prefab";
        private const string FeelPath = "Assets/Settings/UI/RecapFeel.asset";
        private static readonly Vector2 EmptySpace = new(4000f, 4000f);
        private static readonly (int width, int height)[] Sizes = { (1280, 720), (1920, 1080) };
        private const int BannerHoldSpan = 2;
        private const int CardSpan = 6;

        private GameObject hostGo;
        private PreviewHost host;
        private Ship player;
        private SectorSettings bareConfig;
        private string stamp;

        public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;

        public override CaptureConfig Config => new()
        {
            clipName = "parity", width = 1280, height = 720, everyFixedSteps = 5,
            configureView = (camera, light) => { camera.enabled = false; light.enabled = false; }
        };

        public override IEnumerator Run()
        {
            stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            try
            {
                BootHost();
                yield return WaitFor(() => host.Subject != null, "host composed");
                player = hostGo.GetComponentInChildren<PlayerRig>().Player;
                var recap = (RecapPreviewSubject)host.Subject;
                var today = DateTime.UtcNow;
                var none = Array.Empty<string>();
                var victoryRow = new Scoreboard.Row(2, 0, 15f, today);

                // The extraction card leaves the camera free, so it can settle before any outcome holds it.
                recap.Select(RecapOutcomeContext.Extraction(none, 1, 6f));
                host.Transport.Scrub(0, 0f);
                yield return null;
                yield return LookAtEmptySpace(host.Observer);
                yield return FilmStates("extracted");

                var victory = RecapOutcomeContext.Victory(none, 2, 2,
                    new Scoreboard(Array.Empty<Scoreboard.Row>(), victoryRow));
                yield return Show(recap, victory, BannerHoldSpan, "victory-banner");
                yield return Show(recap, victory, CardSpan, "victory-card");

                var defeat = RecapOutcomeContext.Defeat(none, 0, 2,
                    new Scoreboard(new[] { victoryRow }, new Scoreboard.Row(0, 0, 6f, today)),
                    GamePlane.WorldPointToPlane(player.transform.position));
                yield return Show(recap, defeat, BannerHoldSpan, "defeat-banner");
                yield return Show(recap, defeat, CardSpan, "defeat-card");
            }
            finally
            {
                if (hostGo) UnityEngine.Object.DestroyImmediate(hostGo);
                if (bareConfig) UnityEngine.Object.DestroyImmediate(bareConfig);
            }
        }

        private void BootHost()
        {
            bareConfig = ScriptableObject.CreateInstance<SectorSettings>();
            var template = new GameObject("SectorA");
            template.SetActive(false);
            template.AddComponent<Sector>().SetManifest(null, null, Array.Empty<SectorModule>());
            hostGo = new GameObject("ParityHost");
            hostGo.SetActive(false);
            template.transform.SetParent(hostGo.transform);
            host = hostGo.AddComponent<PreviewHost>();
            host.sessionProfile = new SessionProfile
            {
                sectors = new[] { new SectorEntry { prefab = template.GetComponent<Sector>(), config = bareConfig } },
                presentation = true
            };
            host.playerRigPrefab = AssetDatabase.LoadAssetAtPath<PlayerRig>(RigPrefabPath);
            host.observerCamPrefab = AssetDatabase.LoadAssetAtPath<ObserverCam>(ObserverCamPrefabPath);
            host.recapFeel = AssetDatabase.LoadAssetAtPath<RecapFeelProfile>(FeelPath);
            Assert.IsTrue(host.recapFeel, $"feel profile asset at {FeelPath}");
            hostGo.SetActive(true);
        }

        // Nothing in view, as in the baseline: the player stays a framed secondary, not the subject.
        private static IEnumerator LookAtEmptySpace(ObserverCam observer)
        {
            observer.SetSubject(null, removeOldSubject: false);
            observer.SetManualCenter(EmptySpace);
            observer.SetManualZoom(12f);
            yield return WaitFor(() =>
                (GamePlane.WorldPointToPlane(observer.transform.position) - EmptySpace).magnitude < 0.01f
                && Mathf.Abs(observer.Cam.orthographicSize - 12f) < 0.001f, "camera settled on empty space");
        }

        private IEnumerator Show(RecapPreviewSubject recap, RecapOutcomeContext context, int span, string state)
        {
            recap.Select(context);
            host.Transport.Scrub(span, 0f);
            for (var i = 0; i < 3; i++) yield return null;
            yield return FilmStates(state);
        }

        // One episode per size; the recorder starts on the second step.
        private IEnumerator FilmStates(string state)
        {
            foreach (var (width, height) in Sizes)
            {
                Capture.Begin(new CaptureConfig
                {
                    clipName = $"{state}-{width}x{height}", runStamp = stamp, width = width, height = height,
                    everyFixedSteps = 5,
                    configureView = (camera, light) => { camera.enabled = false; light.enabled = false; }
                }, GizmoCaptureProfile.None, new[] { player }, Session.Projectiles);
                for (var i = 0; i < 3; i++) yield return null;
                for (var i = 0; i < 6; i++)
                {
                    FilmStep();
                    yield return null;
                }
                Capture.End();
            }
        }

        private static IEnumerator WaitFor(Func<bool> condition, string what) =>
            AsyncAssert.WaitUntil(condition, 45f, what);
    }
}
#endif

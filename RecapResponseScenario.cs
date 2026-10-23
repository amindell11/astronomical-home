#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Cameras;
using Capture;
using Game;
using Game.Runs;
using NUnit.Framework;
using Substrate;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UI.Hangar;
using UI.Screens;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class RecapResponseScenario : CaptureScenario
{
    public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;
    public override CaptureConfig Config => new() {
        clipName = "recap-response", width = 1280, height = 720, everyFixedSteps = 5,
        configureView = (camera, light) => { camera.enabled = false; light.enabled = false; }
    };

    public override IEnumerator Run()
    {
        var input = new InputTestFixture();
        input.Setup();
        var keyboard = InputSystem.AddDevice<Keyboard>();
        GameHost host = null;
        try
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/InitScene.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return AsyncAssert.WaitUntil(() => UnityEngine.Object.FindFirstObjectByType<HangarScreen>(),
                45f, "hangar opens");
            host = UnityEngine.Object.FindFirstObjectByType<GameHost>();
            typeof(GameHost).GetField("runRecords", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(host, new RunRecordStore("D:/amind/git/agent-4/scratch/capture-runs.jsonl"));
            var hangar = host.GetComponentInChildren<HangarScreen>();
            var launch = (Button)typeof(HangarScreen).GetField("launchButton", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(hangar);
            launch.onClick.Invoke();
            var rig = host.GetComponentInChildren<PlayerRig>();
            yield return AsyncAssert.WaitUntil(() => rig.Player.gameObject.activeInHierarchy && host.ActiveSector,
                45f, "sector loads");
            rig.Player.Damage.SetInvulnerability(100f);
            var observer = host.GetComponentInChildren<ObserverCam>();
            var units = host.GetComponent<UnitService>();
            var enemy = units.SpawnShip(TestAssets.LoadCrimsonPrefab(), null, 1,
                rig.Player.transform.position + GamePlane.Right * 7f + GamePlane.Forward * 5f,
                GamePlane.Rotation, host.ActiveSector.ObstacleField);
            enemy.Damage.SetInvulnerability(100f);
            enemy.Body.linearVelocity = GamePlane.Right * 2f;
            Film(rig.Player);
            var rows = new List<string> { "frame,seconds,alive,hudAlpha,bannerAlpha,detailsAlpha,timeScale,cameraX,cameraY,enemyX,enemyY,continue" };
            for (var frame = 0; frame < 400; frame++)
            {
                yield return new WaitForFixedUpdate();
                if (frame == 75)
                {
                    observer.transform.position += GamePlane.Right * 12f;
                    TestDamage.Kill(rig.Player);
                }
                if (frame == 250) input.Press(keyboard.enterKey);
                if (frame == 255) input.Release(keyboard.enterKey);
                FilmStep();
                var recap = host.GetComponentInChildren<RunRecapScreen>();
                var banner = recap ? recap.transform.Find("OutcomeIntro") : null;
                var details = recap ? recap.transform.Find("Panels/Recap") : null;
                var camera = GamePlane.WorldPointToPlane(observer.transform.position);
                var other = GamePlane.WorldPointToPlane(enemy.transform.position);
                rows.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0},{1:F4},{2},{3:F4},{4:F4},{5:F4},{6:F4},{7:F4},{8:F4},{9:F4},{10:F4},{11}",
                    frame, Time.realtimeSinceStartup, rig.Player.gameObject.activeInHierarchy,
                    rig.Overlay.GetComponent<CanvasGroup>().alpha,
                    banner ? banner.GetComponent<CanvasGroup>().alpha : 0f,
                    details ? details.GetComponent<CanvasGroup>().alpha : 0f,
                    Time.timeScale, camera.x, camera.y, other.x, other.y, frame >= 250));
            }
            File.WriteAllLines(Path.Combine(Capture.FrameDir, "timing.csv"), rows);
            var final = host.GetComponentInChildren<RunRecapScreen>();
            Assert.AreEqual(1f, final.transform.Find("Panels/Recap").GetComponent<CanvasGroup>().alpha);
            Assert.AreEqual(1f, Time.timeScale);
        }
        finally
        {
            if (host) UnityEngine.Object.DestroyImmediate(host.gameObject);
            input.TearDown();
        }
    }
}
#endif

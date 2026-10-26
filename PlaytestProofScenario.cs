#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Capture.Recording;
using Capture.Views;
using Game;
using Game.Preview;
using NUnit.Framework;
using Tests.PlayMode.Common;
using Tests.PlayMode.Recording;
using UI.Screens;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class PlaytestProofScenario : CaptureScenario
{
    public override bool NeedsSession => false;
    public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;

    public override IEnumerator Run()
    {
        var input = new InputTestFixture();
        input.Setup();
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        var bootstrap = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("Game.Preview.PreviewHostBootstrap")).First(type => type != null);
        var host = (PreviewHost)bootstrap.GetMethod("BuildHost", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { PreviewSubjectKind.SectorJump, PreviewMode.Playtest });
        PlayerRig rig = null;
        var phase = 0;
        try
        {
            yield return AsyncAssert.WaitUntil(() =>
            {
                rig = host.Playtest.GetComponentInChildren<PlayerRig>();
                return rig && rig.Player && rig.Tally.SecondsSurvived > 0f;
            }, 45f, "playtest loaded");
            using (var view = (IDisposable)Capture.GetType().GetMethod("PrepareHud1280x720").Invoke(Capture, null))
            using (var recording = Capture.Prepare(new CaptureRequest("results/capture", "preview-playtest", Commit,
                       1280, 720, measurements: new Dictionary<string, Func<double>>
                       {
                           ["phase"] = () => phase,
                           ["player_alive"] = () => rig.Player && rig.Player.gameObject.activeInHierarchy ? 1 : 0
                       })))
            {
                yield return recording.Ready;
                yield return new WaitForSecondsRealtime(1f);
                recording.Start();
                input.Set(mouse.position, new Vector2(640f, 620f));
                input.Press(keyboard.wKey);
                input.Press(mouse.leftButton);
                yield return new WaitForSecondsRealtime(2f);
                input.Release(keyboard.wKey);
                input.Release(mouse.leftButton);
                yield return new WaitForSecondsRealtime(1f);
                phase = 1;
                if (rig.Player.gameObject.activeInHierarchy) TestDamage.Kill(rig.Player);
                RunRecapScreen screen = null;
                yield return AsyncAssert.WaitUntil(() =>
                {
                    screen = host.Playtest.GetComponentInChildren<RunRecapScreen>();
                    return screen && screen.transform.Find("OutcomeIntro/ContinueHint").gameObject.activeInHierarchy;
                }, 20f, "death banner ready");
                yield return new WaitForSecondsRealtime(1f);
                phase = 2;
                input.Press(keyboard.tKey);
                yield return null;
                input.Release(keyboard.tKey);
                yield return AsyncAssert.WaitUntil(() => screen.transform.Find("Panels/Recap").GetComponent<CanvasGroup>().interactable,
                    10f, "breakdown ready");
                yield return new WaitForSecondsRealtime(2f);
                Result = recording.Finish();
                Assert.That(Result.FrameCount, Is.GreaterThan(10));
            }
            yield return host.Shutdown();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host.gameObject);
            input.TearDown();
        }
    }
}

#endif

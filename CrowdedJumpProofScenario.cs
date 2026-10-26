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
using Game.Travel;
using NUnit.Framework;
using Objectives;
using Substrate;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using Tests.PlayMode.Recording;
using UI.Screens;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class CrowdedJumpProofScenario : CaptureScenario
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
        PreviewHost host = null;
        PlayerRig rig = null;
        SectorJumpFlight jump = null;
        var phase = 0;
        try
        {
            using (var view = (IDisposable)Capture.GetType().GetMethod("PrepareHud1280x720").Invoke(Capture, null))
            using (var recording = Capture.Prepare(new CaptureRequest("results/capture", "crowded-jump-proof", Commit,
                       1280, 720, measurements: new Dictionary<string, Func<double>>
                       {
                           ["phase"] = () => phase,
                           ["jump_active"] = () => jump ? 1 : 0,
                           ["jump_speed"] = () => jump ? jump.State.Speed : 0,
                           ["jump_distance"] = () => jump ? jump.State.Distance : 0,
                           ["locale_blend"] = () => jump ? jump.State.LocaleBlend : phase >= 5 ? 1 : 0,
                           ["active_locale"] = () => SceneManager.GetActiveScene().name switch
                           {
                               "Locale_3" => 3,
                               "Locale_1" => 1,
                               _ => 0
                           },
                           ["ship_speed"] = () => rig && rig.Player ? rig.Player.Body.linearVelocity.magnitude : 0,
                           ["ship_velocity_x"] = () => rig && rig.Player ? rig.Player.Body.linearVelocity.x : 0,
                           ["ship_velocity_y"] = () => rig && rig.Player ? rig.Player.Body.linearVelocity.y : 0
                       })))
            {
                yield return recording.Ready;
                var bootstrap = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("Game.Preview.PreviewHostBootstrap")).First(type => type != null);
                host = (PreviewHost)bootstrap.GetMethod("BuildHost", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[]
                    {
                        PreviewSubjectKind.SectorJump, PreviewMode.Playtest, PreviewPlaytestSetup.CrowdedJumpGate
                    });
                input.Set(mouse.position, new Vector2(640f, 620f));
                yield return AsyncAssert.WaitUntil(() =>
                {
                    if (!host.Playtest) return false;
                    rig = host.Playtest.GetComponentInChildren<PlayerRig>();
                    return rig && rig.Player && rig.Tally.SecondsSurvived > 0f;
                }, 45f, "crowded Playtest flight loaded");

                var game = host.Playtest;
                var player = rig.Player;
                var zone = game.ActiveSector.GetComponentInChildren<ExtractionZone>();
                Assert.That(game.GetComponent<UnitService>().ActiveRegistry.ActiveShips.Count(ship => ship != player),
                    Is.GreaterThan(0), "the departure must contain active pursuers");
                phase = 1;
                recording.Start();
                input.Press(keyboard.wKey);
                yield return new WaitForSecondsRealtime(.2f);
                input.Release(keyboard.wKey);
                yield return new WaitForSecondsRealtime(.8f);
                Assert.That(player && player.gameObject.activeInHierarchy, Is.True);
                Assert.That(zone.IsPlayerInZone, Is.True, "the fresh F press must occur inside the jump gate");

                phase = 2;
                input.Press(keyboard.fKey);
                InputSystem.Update();
                jump = game.Jump;
                Assert.That(jump, Is.Not.Null, "F commits before returning from the input callback");
                Assert.That(player.Commander.enabled || player.Movement.enabled, Is.False);
                Assert.That(player.Colliders.Any(collider => collider.enabled), Is.False);
                input.Release(keyboard.fKey);
                InputSystem.Update();
                RunRecapScreen recap = null;
                yield return AsyncAssert.WaitUntil(() =>
                {
                    recap = game.GetComponentInChildren<RunRecapScreen>();
                    return jump.Holding && recap;
                }, 30f, "hyperspace extraction recap opened");

                phase = 3;
                var heldDistance = jump.State.Distance;
                var direction = jump.State.JumpDirection;
                yield return new WaitForSecondsRealtime(2f);
                Assert.That(jump.Holding, Is.True);
                Assert.That(jump.State.Distance, Is.GreaterThan(heldDistance));
                Assert.That(jump.State.LocaleBlend, Is.Zero);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Locale_3"));

                phase = 4;
                var button = recap.GetComponentInChildren<Button>();
                Assert.That(button.interactable, Is.True);
                var rect = (RectTransform)button.transform;
                Canvas.ForceUpdateCanvases();
                input.Set(mouse.position,
                    RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)));
                yield return null;
                input.Press(mouse.leftButton);
                yield return null;
                input.Release(mouse.leftButton);
                yield return null;
                input.Set(mouse.position, new Vector2(640f, 360f) + direction * 250f);
                yield return AsyncAssert.WaitUntil(() => !game.Jump, 30f, "Continue completes the arrival");
                Assert.That(rig.Player, Is.SameAs(player));
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Locale_1"));
                Assert.That(player.Commander.enabled && player.Movement.enabled, Is.True);
                var worldDirection = GamePlane.PlaneDirToWorld(direction);
                Assert.That(Vector3.Dot(player.Body.linearVelocity, worldDirection), Is.GreaterThan(1f));

                phase = 5;
                yield return new WaitForSecondsRealtime(.6f);
                var coastSpeed = Vector3.Dot(player.Body.linearVelocity, worldDirection);
                phase = 6;
                input.Press(keyboard.sKey);
                yield return new WaitForSecondsRealtime(.6f);
                input.Release(keyboard.sKey);
                Assert.That(Vector3.Dot(player.Body.linearVelocity, worldDirection), Is.LessThan(coastSpeed),
                    "restored controls brake the arrival momentum");
                Result = recording.Finish();
                Assert.That(Result.FrameCount, Is.GreaterThan(10));
            }
            yield return host.Shutdown();
        }
        finally
        {
            if (host) UnityEngine.Object.DestroyImmediate(host.gameObject);
            input.TearDown();
        }
    }
}
#endif

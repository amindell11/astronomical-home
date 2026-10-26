#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Capture.Recording;
using Capture.Views;
using Game;
using Tests.PlayMode.Bootstrap;
using Tests.PlayMode.Recording;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class ProductionJumpProofScenario : CaptureScenario
{
    public override bool NeedsSession => false;
    public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;

    public override IEnumerator Run()
    {
        var test = new MultiSectorRunPlayModeTests();
        test.SetUp();
        var previous = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        GameHost host = null;
        PlayerRig rig = null;
        try
        {
            using var view = (IDisposable)Capture.GetType().GetMethod("PrepareHud1280x720").Invoke(Capture, null);
            using var recording = Capture.Prepare(new CaptureRequest("results/capture", "production-jump-proof", Commit,
                1280, 720, measurements: new Dictionary<string, Func<double>>
                {
                    ["jump_active"] = () => host && host.Jump ? 1 : 0,
                    ["locale_blend"] = () => host && host.Jump ? host.Jump.State.LocaleBlend : 0,
                    ["ship_speed"] = () => rig && rig.Player ? rig.Player.Body.linearVelocity.magnitude : 0,
                    ["controls_enabled"] = () => rig && rig.Player && rig.Player.Commander.enabled ? 1 : 0
                }));
            yield return recording.Ready;
            recording.Start();
            var run = test.ProductionJump_LaunchesNormally_HoldsRecap_ArrivesMoving_ThenWinsWithoutAnotherJump();
            var pending = new Stack<IEnumerator>();
            pending.Push(run);
            while (pending.Count > 0)
            {
                var current = pending.Peek();
                if (!current.MoveNext()) { pending.Pop(); continue; }
                host = UnityEngine.Object.FindFirstObjectByType<GameHost>();
                rig = host ? host.GetComponentInChildren<PlayerRig>() : null;
                if (current.Current is IEnumerator nested) pending.Push(nested);
                else yield return current.Current;
            }
            Result = recording.Finish();
        }
        finally
        {
            InputSystem.settings.backgroundBehavior = previous;
            test.TearDown();
        }
    }
}
#endif

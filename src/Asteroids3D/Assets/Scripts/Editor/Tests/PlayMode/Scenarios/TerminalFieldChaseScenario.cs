#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using AI;
using AI.Navigation.MPC;
using Game;
using Game.Capture;
using NUnit.Framework;
using RL.Arena;
using RL.Opponents;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;

namespace Tests.PlayMode.Scenarios
{
    public sealed class TerminalFieldChaseScenario : CaptureScenario
    {
        public override GizmoCaptureProfile Profile => GizmoCaptureProfile.Steering;
        public override CaptureConfig Config => new()
        {
            clipName = "terminal-field-dense-chase-" + (FieldOff ? "off" : "on"),
            outputRoot = Path.Combine(Environment.GetEnvironmentVariable("MPC_FIELD_OUT") ?? Path.GetFullPath("../../results/mpc-rig/terminal-field-independent"), "capture"),
            gizmoScope = CaptureGizmoScope.Team,
            gizmoScopeTeam = 0,
            minHalfHeight = 35f,
        };
        private static bool FieldOff => Environment.GetEnvironmentVariable("MPC_FIELD_CAPTURE_OFF") == "1";

        public override IEnumerator Run()
        {
            const int seed = 3301;
            const int steps = 2000;
            var assets = AssetDatabase.LoadAssetAtPath<HarnessAssets>(HarnessAssets.AssetPath);
            using var field = HarnessField.Spawn(Vector2.zero, assets, 2f, presentationEnabled: false);
            var start = new Vector2(0f, -25f);
            var targetStart = new Vector2(0f, 25f);
            field.Rebuild(seed, start, targetStart);
            yield return new WaitForFixedUpdate();
            var units = Session.Services.UnitService;
            var pursuer = units.SpawnShip(assets.ShipPrefab, assets.AgentPilot, 0,
                GamePlane.PlanePointToWorld(start), GamePlane.Rotation, field.Field);
            var evader = units.SpawnShip(assets.ShipPrefab, assets.BaselinePilot, 1,
                GamePlane.PlanePointToWorld(targetStart), GamePlane.Rotation, field.Field);
            var nav = pursuer.GetComponentInChildren<Navigator>();
            var settings = UnityEngine.Object.Instantiate(nav.mpcSettings);
            if (FieldOff) settings.wTerminalField = 0f;
            var original = nav.mpcSettings;
            try
            {
                nav.mpcSettings = settings;
                nav.ResetState();
                pursuer.GetComponentInChildren<AICommander>().InstallBrain<TerminalFieldChaseBrain>().Initialize(evader);
                evader.GetComponentInChildren<AICommander>().InstallBrain<ArchetypeBrain>().Configure(pursuer,
                    OpponentArchetype.Evader, new OpponentDraw { speedFraction = 0.85f, jukePeriod = 1.2f },
                    seed, Vector2.zero, 120f);
                var contacts = pursuer.gameObject.AddComponent<TerminalFieldContactCounter>();
                Film(pursuer, evader);
                var outDir = Environment.GetEnvironmentVariable("MPC_FIELD_OUT") ?? Path.GetFullPath("../../results/mpc-rig/terminal-field-independent");
                Directory.CreateDirectory(outDir);
                using var report = new StreamWriter(Path.Combine(outDir,
                    $"chase-{(FieldOff ? "off" : "on")}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.csv"));
                report.WriteLine("seconds,range,pathLength,collisionSteps,speed,endpointExcess,bakeCount,spacing,renderFrame,updateFrame,captureDelta,actualSeconds");
                var previous = start;
                var path = 0f;
                var samples = 0;
                var startFixedTime = Time.fixedTimeAsDouble;
                for (var i = 0; i < steps && pursuer && evader && pursuer.gameObject.activeInHierarchy && evader.gameObject.activeInHierarchy; i++)
                {
                    yield return new WaitForFixedUpdate();
                    var position = pursuer.Kinematics.pos;
                    path += Vector2.Distance(previous, position);
                    previous = position;
                    var owner = nav.mpc.TerminalField;
                    var endpoint = nav.mpc.PredictedStates[nav.mpc.PredictedStates.Length - 1].pos;
                    report.WriteLine(FormattableString.Invariant($"{(i + 1) * Time.fixedDeltaTime},{Vector2.Distance(position, evader.Kinematics.pos)},{path},{contacts.Steps},{pursuer.Kinematics.vel.magnitude},{owner.View.Sample(endpoint)},{owner.BakeCount},{owner.View.spacing},{Time.renderedFrameCount},{Time.frameCount},{Time.captureDeltaTime},{Time.fixedTimeAsDouble - startFixedTime}"));
                    if (i % 50 == 0) report.Flush();
                    FilmStep();
                    samples++;
                }
                Assert.That(samples, Is.GreaterThan(0));
                Assert.That(nav.mpc.TerminalField.BakeCount, Is.GreaterThan(1));
            }
            finally
            {
                if (nav)
                {
                    nav.mpcSettings = original;
                    nav.ResetState();
                }
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }
    }
}
#endif

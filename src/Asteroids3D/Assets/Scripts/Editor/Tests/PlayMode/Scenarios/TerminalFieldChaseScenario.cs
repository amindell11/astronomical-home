#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using AI;
using AI.Context;
using AI.Strategy;
using Capture;
using NUnit.Framework;
using RL.Arena;
using RL.Opponents;
using Ships;
using Substrate;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;

namespace Tests.PlayMode.Scenarios
{
    /// <summary>Verification footage for the terminal field (#461) on the case that motivated it: the agent pilot chases an Evader through a density-2.0 harness field, gizmos scoped to the pursuing team. `MPC_FIELD_CAPTURE_OFF=1` clones the settings with the field weight zeroed for the paired off-arm clip.</summary>
    public sealed class TerminalFieldChaseScenario : CaptureScenario
    {
        private const int Seed = 3301;
        private const float Density = 2.0f;
        private const float SimSeconds = 40f;
        private const float CloseoutRing = 6f;
        private const float ClosingSpeed = 8f;

        private static bool FieldOff => Environment.GetEnvironmentVariable("MPC_FIELD_CAPTURE_OFF") == "1";

        /// <summary>The scenario's fixed pursuit sentence, re-issued every tick.</summary>
        private sealed class ChaseSentenceBrain : Brain
        {
            private Ship target;

            public void Configure(Ship target) => this.target = target;

            public override BrainDecision? Decide(AIContext ctx)
            {
                if (!target || !target.gameObject.activeInHierarchy) return null;
                var nav = NavObjective.Anchored(target.Id)
                    .Facing(0f, 1f)
                    .Position(0f, 0f, CloseoutRing, 1f)
                    .Velocity(ClosingSpeed, 0f, 0.5f)
                    .Field(1f);
                return new BrainDecision(nav, false, false);
            }
        }

        public override GizmoCaptureProfile Profile => GizmoCaptureProfile.Steering;

        public override CaptureConfig Config => new()
        {
            clipName = GetType().Name + (FieldOff ? "-field-off" : "-field-on"),
            outputRoot = Path.GetFullPath("../../results/mpc-rig/terminal-field/capture"),
            gizmoScope = CaptureGizmoScope.Team,
            gizmoScopeTeam = 0,
            minHalfHeight = 35f,
        };

        public override IEnumerator Run()
        {
            var assets = AssetDatabase.LoadAssetAtPath<HarnessAssets>(HarnessAssets.AssetPath);
            Assert.IsNotNull(assets, $"HarnessAssets missing at {HarnessAssets.AssetPath}");

            var start = new Vector2(0f, -25f);
            var evaderStart = new Vector2(0f, 25f);
            using var rocks = HarnessField.Spawn(Vector2.zero, assets, Density, parent: null, presentationEnabled: false);
            rocks.Rebuild(Seed, start, evaderStart);
            // The field builds in its own Start; let it land before anyone scans it.
            yield return null;
            yield return null;

            var units = Session.Services.UnitService;
            var pursuer = units.SpawnShip(assets.ShipPrefab, assets.AgentPilot, 0,
                Session.Frame.Place(start), GamePlane.Rotation, rocks.Field);
            var evader = units.SpawnShip(assets.ShipPrefab, assets.BaselinePilot, 1,
                Session.Frame.Place(evaderStart), GamePlane.Rotation, rocks.Field);
            Assert.IsNotNull(pursuer, "Failed to spawn the pursuing ship");
            Assert.IsNotNull(evader, "Failed to spawn the evading ship");

            var nav = pursuer.GetComponentInChildren<Navigator>();
            var authored = nav.mpcSettings;
            var arm = UnityEngine.Object.Instantiate(authored);
            if (FieldOff) arm.wTerminalField = 0f;
            try
            {
                nav.mpcSettings = arm;
                nav.ResetState();
                pursuer.GetComponentInChildren<AICommander>().InstallBrain<ChaseSentenceBrain>().Configure(evader);
                evader.GetComponentInChildren<AICommander>().InstallBrain<ArchetypeBrain>().Configure(pursuer,
                    OpponentArchetype.Evader, new OpponentDraw { speedFraction = 0.85f, jukePeriod = 1.2f },
                    Seed, Vector2.zero, borderRadius: 120f);
                Film(pursuer, evader);

                var steps = Mathf.CeilToInt(SimSeconds / Time.fixedDeltaTime);
                for (var i = 0; i < steps && pursuer && evader
                     && pursuer.gameObject.activeInHierarchy && evader.gameObject.activeInHierarchy; i++)
                {
                    yield return new WaitForFixedUpdate();
                    FilmStep();
                }
                Assert.That(nav.mpc.TerminalField.BakeCount, Is.GreaterThan(1), "the pursuer re-baked its field over the chase");
            }
            finally
            {
                if (nav)
                {
                    nav.mpcSettings = authored;
                    nav.ResetState();
                }
                UnityEngine.Object.DestroyImmediate(arm);
            }
        }
    }
}
#endif

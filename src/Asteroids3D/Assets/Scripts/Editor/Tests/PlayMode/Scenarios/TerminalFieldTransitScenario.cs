#if UNITY_EDITOR
using System.Collections;
using AI;
using AI.Context;
using AI.Strategy;
using Game;
using Game.Capture;
using Game.RLHarness;
using NUnit.Framework;
using Ships;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;

namespace Tests.PlayMode.Scenarios
{
    /// <summary>Verification footage for the terminal field (#461): a closing hand sentence (AIM, POS ring, VEL radial 8 at 0.5, FIELD 1) across a density-2.0 harness rock field toward a stationary Dummy 140 m away, filmed with the Steering gizmos scoped to the transiting ship so the field grid, its detour-excess heat and the endpoint charge are on screen beside the predicted path. The VEL slot matters: from rest, aligned with a goal this far, a POS ring alone never produces a candidate that beats the zero-control incumbent.</summary>
    public sealed class TerminalFieldTransitScenario : CaptureScenario
    {
        private const float Density = 2.0f;
        private const float HalfGap = 70f;
        private const float SimSeconds = 30f;
        private const float DoneRange = 10f;
        private const float CloseoutRing = 6f;
        private const float ClosingSpeed = 8f;
        private const int LayoutSeed = 2001;

        /// <summary>The scenario's fixed closing sentence, re-issued every tick like <see cref="SentenceBrain"/>.</summary>
        private sealed class ClosingSentenceBrain : Brain
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
            clipName = GetType().Name,
            gizmoScope = CaptureGizmoScope.Selected,
        };

        public override IEnumerator Run()
        {
            var assets = AssetDatabase.LoadAssetAtPath<HarnessAssets>(HarnessAssets.AssetPath);
            Assert.IsNotNull(assets, $"HarnessAssets missing at {HarnessAssets.AssetPath}");

            var start = new Vector2(-HalfGap, 0f);
            var goal = new Vector2(HalfGap, 0f);
            using var rocks = HarnessField.Spawn(Vector2.zero, assets, Density, parent: null, presentationEnabled: false);
            rocks.Rebuild(LayoutSeed, start, goal);
            // The field builds in its own Start; let it land before anyone scans it.
            yield return null;
            yield return null;

            var (agent, agentCmdr) = Spawn(assets, start, rotDeg: -90f, team: 0, rocks);
            var (target, targetCmdr) = Spawn(assets, goal, rotDeg: 90f, team: 1, rocks);
            targetCmdr.InstallBrain<ArchetypeBrain>()
                .Configure(agent, OpponentArchetype.Dummy, default, jukeSeed: 0, Vector2.zero, borderRadius: 0f);
            agentCmdr.InstallBrain<ClosingSentenceBrain>().Configure(target);
            Film(agent);

            var steps = Mathf.CeilToInt(SimSeconds / Time.fixedDeltaTime);
            for (var i = 0; i < steps && agent && target; i++)
            {
                yield return new WaitForFixedUpdate();
                FilmStep();
                if (Vector2.Distance(agent.Kinematics.pos, target.Kinematics.pos) < DoneRange) break;
            }
        }

        private (Ship ship, AICommander cmdr) Spawn(HarnessAssets assets, Vector2 planePos, float rotDeg, int team, HarnessField rocks)
        {
            var ship = Session.Services.UnitService.SpawnShip(
                assets.ShipPrefab, assets.AgentPilot, team,
                Session.Frame.Place(planePos),
                GamePlane.Rotation * Quaternion.AngleAxis(rotDeg, Vector3.forward),
                rocks.Field);
            Assert.IsNotNull(ship, "Failed to spawn a scenario ship from the harness assets");
            var cmdr = ship.GetComponentInChildren<AICommander>();
            Assert.IsNotNull(cmdr, "Scenario ship is missing an AICommander");
            return (ship, cmdr);
        }
    }
}
#endif

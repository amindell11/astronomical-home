#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Ships.Command;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// Commander-path determinism: unlike <see cref="MpcSolverTests"/>, which injects a seed
    /// straight into the solver, these drive the real spawn → <see cref="AI.AICommander"/> init
    /// path so a regression in the commander's seed wiring (e.g. seeding off object identity) is
    /// caught. Two agents reconstructed with the same explicit decision seed must plan identically;
    /// the combat-exit timer must advance on accumulated sim-time, not wall-clock.
    /// </summary>
    [TestFixture]
    [Category("AI")]
    [Category("Slow")]
    public class AiCommanderDeterminismPlayModeTests : AIIntegrationFixture
    {
        private static readonly Vector2 Goal = new(30f, 12f);
        private const int SolvesPerPlan = 5;

        private static bool CommandsEqual(IReadOnlyList<PilotCommand> a, IReadOnlyList<PilotCommand> b)
        {
            if (a.Count != b.Count) return false;
            for (var i = 0; i < a.Count; i++)
                if (a[i].thrust != b[i].thrust || a[i].strafe != b[i].strafe ||
                    a[i].boost != b[i].boost || a[i].yawTorque != b[i].yawTorque)
                    return false;
            return true;
        }

        // Drives the commander-initialized navigator through a fixed number of solves at the ship's
        // spawn pose (no physics step, so the pose — and thus the solver's position hash — is fixed).
        private PilotCommand[] CapturePlan(int decisionSeed)
        {
            var (_, cmdr) = CreateAIShip(Vector3.zero, team: 0, decisionSeed: decisionSeed);
            cmdr.Navigator.SetNavigationPoint(Goal);
            var plan = new PilotCommand[SolvesPerPlan];
            for (var i = 0; i < SolvesPerPlan; i++)
                plan[i] = cmdr.Navigator.ComputeCommand();
            return plan;
        }

        [UnityTest]
        public IEnumerator CommanderPath_SameDecisionSeed_DistinctShips_PlanIdentically()
        {
            // Two separately-spawned ships (distinct GetInstanceID) with the same explicit seed.
            // Identical plans are only possible if the commander seeds off Ship.DecisionSeed, not
            // object identity — the exact regression the shelved attempt shipped.
            var planA = CapturePlan(4242);
            var planB = CapturePlan(4242);

            Assert.IsTrue(CommandsEqual(planA, planB),
                "Two agents reconstructed with the same decision seed must plan identically.");
            yield break;
        }

        [UnityTest]
        public IEnumerator CombatExit_TimerAdvancesOnSimTime_NotWallClock()
        {
            var (_, cmdr) = CreateAIShip(Vector3.zero, team: 0);
            var (enemy, _) = CreateAIShip(new Vector3(12f, 0f, 0f), team: 1);

            var deadline = Time.realtimeSinceStartup + 5f;
            while (cmdr.context?.Combat.HasEnemy != true &&
                   Time.realtimeSinceStartup < deadline)
                yield return new WaitForFixedUpdate();

            var ctx = cmdr.context;
            Assert.IsTrue(ctx?.Combat.HasEnemy == true, "Enemy was not acquired within timeout.");

            // Freeze the real loop so only the dt we feed advances sim-time.
            cmdr.enabled = false;
            enemy.gameObject.SetActive(false);

            ctx.Update(0.05f);
            Assert.IsFalse(ctx.Combat.HasEnemy, "Deactivated enemy must drop HasEnemy.");
            var afterLoss = ctx.Combat.TimeSinceCombat;

            // Pause: dt == 0 must freeze the timer. A wall-clock timer would keep growing here.
            for (var i = 0; i < 30; i++) ctx.Update(0f);
            Assert.AreEqual(afterLoss, ctx.Combat.TimeSinceCombat, 1e-4f,
                "TimeSinceCombat must not advance while dt is 0 (proves sim-time, not wall-clock).");

            // Fed dt accumulates exactly.
            ctx.Update(0.5f);
            Assert.AreEqual(afterLoss + 0.5f, ctx.Combat.TimeSinceCombat, 1e-3f,
                "TimeSinceCombat must accumulate the fed dt exactly.");

            // Enough accumulated sim-time exits combat regardless of the configured delay.
            ctx.Update(1000f);
            Assert.IsFalse(ctx.Combat.InCombat, "Large accumulated sim-time must exit combat.");

            // Re-acquiring the enemy resets the grace timer.
            enemy.gameObject.SetActive(true);
            ctx.Update(0.05f);
            Assert.IsTrue(ctx.Combat.HasEnemy, "Re-activated enemy must be re-acquired.");
            Assert.AreEqual(0f, ctx.Combat.TimeSinceCombat, "Re-acquisition must reset TimeSinceCombat.");
            Assert.IsTrue(ctx.Combat.InCombat, "Re-acquired enemy must be in combat.");
        }
    }
}
#endif

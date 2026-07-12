#if UNITY_EDITOR
using Movement.MPC;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the <see cref="Cost.Evaluate"/> grouping: Evaluate must equal the per-term inspector breakdown, and disabling tactics must drop exactly the authored-tactics block (LOS / exposure / tangential / miss-distance) and nothing else.</summary>
    [Category("MPC")]
    public class MpcCostRegroupEditModeTests
    {
        // Every cost group live (enemy, obstacles, projectile lead, terminal ramp) so the regroup is exercised across all terms at once.
        private static Config RichConfig() => new()
        {
            dt = 0.1f, invDt = 10f, horizon = 17,
            wPos = 1f, wVel = 0.5f, wClosing = 1f, closingFadeDistance = 10f,
            wYaw = 0.5f, wYawDistanceScale = 1f, wYawRate = 0.1f,
            positionCurve = 2f, positionSaturationDistance = 35f,
            terminalMultiplier = 10f, terminalCurve = 1f,
            wEffort = 0.05f, wSmoothnessThrust = 0.5f, wSmoothnessStrafe = 5f, wSmoothnessYaw = 0.2f,
            wMomentum = 0.3f, wBoostEffort = 0.5f,
            wFacing = 1f, facingWidth = 0.5f, facingTarget = float.NaN,
            wLos = 1f, wExposure = 1f, exposureWidth = 0.5f, wTangential = 1f, wMissDistance = 1f,
            wObstacle = 5f, collisionPenalty = 10000f, collisionSafetyMargin = 0.3f,
            arrivalDistance = 3f, arrivalDistanceSq = 9f, arrivalVelScale = 5f, arrivalYawScale = 0.1f,
            maxBankAngleRad = 35f * Mathf.Deg2Rad, maxSpeedSq = 900f, maxYawRateSq = 100f,
            shipRadius = 1.4f, maxLatAccel = 6f,
            goalMode = GoalMode.MaintainRange, desiredRange = 20f, rangeTolerance = 5f,
            tacticalEnabled = true,
        };

        private static readonly State RichState = new()
        {
            pos = new float2(5f, -3f), vel = new float2(4f, 8f), yaw = 0.3f, yawRate = 0.5f,
        };
        private static readonly Control U = new() { thrust = 0.6f, strafe = 0.4f, yawTorque = -0.3f, boost = 1f };
        private static readonly Control PrevU = new() { thrust = 0.2f, strafe = -0.1f, yawTorque = 0.1f, boost = 0f };
        private const int Step = 8;   // mid-horizon → terminal ramp active (t = 0.5)

        // Enemy well clear of the ship, obstacles ahead-but-not-overlapping so turn-away is live and the collision penalty is not.
        private static void WithInput(System.Action<CostInput> body)
        {
            var obstacles = new NativeArray<ObstacleData>(2, Allocator.Temp);
            try
            {
                obstacles[0] = new ObstacleData { position = new float2(5f, 20f), radius = 2f, weight = 1f };
                obstacles[1] = new ObstacleData { position = new float2(30f, 0f), radius = 3f, weight = 1f };
                body(new CostInput
                {
                    goalPos = new float2(25f, 10f),
                    obstacles = obstacles,
                    obstacleCount = obstacles.Length,
                    enemyPos = new float2(25f, 10f),
                    enemyVel = new float2(-2f, 1f),
                    enemyYaw = 1.0f,
                    enemyYawRate = 0.2f,
                    projectileSpeed = 50f,
                    initialVel = new float2(1f, 2f),
                });
            }
            finally
            {
                obstacles.Dispose();
            }
        }

        private static float TerminalRamp(Config cfg, int step) =>
            (cfg.terminalMultiplier > 0f && cfg.horizon > 1)
                ? math.pow(step / (float)(cfg.horizon - 1), cfg.terminalCurve) * cfg.terminalMultiplier
                : 0f;

        [Test]
        public void Evaluate_MatchesInspectorBreakdownTotal()
        {
            WithInput(input =>
            {
                var cfg = RichConfig();
                var eval = Cost.Evaluate(RichState, U, PrevU, input, cfg, false, Step);
                var breakdown = Cost.EvaluateBreakdown(RichState, U, PrevU, input, cfg, false, Step);

                Assert.That(eval, Is.EqualTo(breakdown.total).Within(0.05f).Percent,
                    "Regrouped Evaluate must equal the per-term inspector breakdown total — the " +
                    "two summations must not drift. (Tolerance absorbs float32 re-association only; " +
                    "a dropped/mis-ramped term shifts the total far more.)");
            });
        }

        [Test]
        public void TacticalDisabled_DropsExactlyTheAuthoredTacticsBlock()
        {
            WithInput(input =>
            {
                var on = RichConfig();
                var off = RichConfig();
                off.tacticalEnabled = false;

                var breakdown = Cost.EvaluateBreakdown(RichState, U, PrevU, input, on, false, Step);
                var authoredTactics = breakdown.los + breakdown.exposure + breakdown.tangential + breakdown.missDistance;
                Assert.That(authoredTactics, Is.GreaterThan(0f),
                    "Fixture must actually exercise the tactical block, else the guard is vacuous.");

                var eOn = Cost.Evaluate(RichState, U, PrevU, input, on, false, Step);
                var eOff = Cost.Evaluate(RichState, U, PrevU, input, off, false, Step);

                // The block is ramped, so disabling it removes (1 + ramp) × its weighted sum.
                var expectedDrop = (1f + TerminalRamp(on, Step)) * authoredTactics;
                Assert.That(eOn - eOff, Is.EqualTo(expectedDrop).Within(0.1f).Percent,
                    "Disabling tactics must drop exactly the ramped authored-tactics contribution.");
            });
        }

        [Test]
        public void TacticalDisabled_KeepsAimObjectiveAndFeasibility()
        {
            WithInput(input =>
            {
                // Zero the four authored-tactics weights but keep facing (aim) live: toggling tacticalEnabled must then be a no-op.
                var on = RichConfig();
                on.wLos = 0f; on.wExposure = 0f; on.wTangential = 0f; on.wMissDistance = 0f;
                var off = on;
                off.tacticalEnabled = false;

                var eOn = Cost.Evaluate(RichState, U, PrevU, input, on, false, Step);
                var eOff = Cost.Evaluate(RichState, U, PrevU, input, off, false, Step);

                Assert.That(eOff, Is.EqualTo(eOn),
                    "With no authored tactics active, the tacticalEnabled toggle must not touch " +
                    "aim/objective/feasibility.");
            });
        }
    }
}
#endif

#if UNITY_EDITOR
using Movement.MPC;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the <see cref="Cost.Evaluate"/> grouping: Evaluate must equal the per-term inspector breakdown total — the two summations must not drift.</summary>
    [Category("MPC")]
    public class MpcCostRegroupEditModeTests
    {
        // Every surviving cost group live (tracker, aim, regularizers, control terms, obstacles, terminal ramp) so the identity is exercised across all terms at once.
        private static Config RichConfig() => new()
        {
            dt = 0.1f, invDt = 10f, horizon = 17,
            wYawRate = 0.1f,
            terminalMultiplier = 10f, terminalCurve = 1f,
            wEffort = 0.05f, wSmoothnessThrust = 0.5f, wSmoothnessStrafe = 5f, wSmoothnessYaw = 0.2f,
            wMomentum = 0.3f, wBoostEffort = 0.5f,
            wFacing = 1f, facingWidth = 0.5f, facingTarget = float.NaN,
            wObstacle = 5f, collisionPenalty = 10000f, collisionSafetyMargin = 0.3f,
            maxBankAngleRad = 35f * Mathf.Deg2Rad, maxSpeedSq = 900f, maxYawRateSq = 100f,
            shipRadius = 1.4f, maxLatAccel = 6f,
            wVelTrack = 5f,
        };

        private static readonly State RichState = new()
        {
            pos = new float2(5f, -3f), vel = new float2(4f, 8f), yaw = 0.3f, yawRate = 0.5f,
        };
        private static readonly Control U = new() { thrust = 0.6f, strafe = 0.4f, yawTorque = -0.3f, boost = 1f };
        private static readonly Control PrevU = new() { thrust = 0.2f, strafe = -0.1f, yawTorque = 0.1f, boost = 0f };
        private const int Step = 8;   // mid-horizon → terminal ramp active (t = 0.5)

        // Enemy well clear of the ship; the anchored facing channel (offset 0, full authority) arms intercept-facing; initialVel off the velocity axis keeps momentum live.
        private static void WithInput(ObstacleData[] obstacleData, System.Action<CostInput> body)
        {
            var obstacles = new NativeArray<ObstacleData>(obstacleData, Allocator.Temp);
            try
            {
                body(new CostInput
                {
                    velocityReference = new float2(2f, -6f),
                    obstacles = obstacles,
                    obstacleCount = obstacles.Length,
                    enemyPos = new float2(25f, 10f),
                    enemyVel = new float2(-2f, 1f),
                    enemyYaw = 1.0f,
                    enemyYawRate = 0.2f,
                    projectileSpeed = 50f,
                    initialVel = new float2(2f, -1f),
                    anchored = new AnchoredIntent { hasFacing = true, facingOffsetRad = 0f, facingWeight = 1f },
                });
            }
            finally
            {
                obstacles.Dispose();
            }
        }

        private static ObstacleData Obstacle(float x, float y) => new()
        {
            position = new float2(x, y), radius = 2f, weight = 1f,
        };

        [Test]
        public void Evaluate_MatchesInspectorBreakdownTotal()
        {
            // On-path-but-clear obstacle → turn-away branch; off-path obstacle rides along free.
            WithInput(new[] { Obstacle(7f, 1f), Obstacle(5f, 20f) }, input =>
            {
                var cfg = RichConfig();
                var eval = Cost.Evaluate(RichState, U, PrevU, input, cfg, Step);
                var breakdown = Cost.EvaluateBreakdown(RichState, U, PrevU, input, cfg, Step);

                Assert.That(breakdown.velocityTrack, Is.GreaterThan(0f), "Fixture must exercise the tracker.");
                Assert.That(breakdown.facing, Is.GreaterThan(0f), "Fixture must exercise intercept-facing.");
                Assert.That(breakdown.yawRate, Is.GreaterThan(0f), "Fixture must exercise yaw-rate damping.");
                Assert.That(breakdown.obstacle, Is.GreaterThan(0f), "Fixture must exercise turn-away.");
                Assert.That(breakdown.momentum, Is.GreaterThan(0f), "Fixture must exercise momentum.");
                Assert.That(breakdown.effort, Is.GreaterThan(0f), "Fixture must exercise effort.");
                Assert.That(breakdown.boostEffort, Is.GreaterThan(0f), "Fixture must exercise boost effort.");
                Assert.That(breakdown.smoothness, Is.GreaterThan(0f), "Fixture must exercise smoothness.");
                Assert.That(breakdown.collision, Is.EqualTo(0f), "A clear hull must not pay the collision penalty.");

                Assert.That(eval, Is.EqualTo(breakdown.total).Within(0.05f).Percent,
                    "Evaluate must equal the per-term inspector breakdown total. (Tolerance absorbs " +
                    "float32 re-association only; a dropped/mis-ramped term shifts the total far more.)");
            });
        }

        [Test]
        public void Evaluate_MatchesInspectorBreakdownTotal_InCollision()
        {
            // Overlapping hull → the fixed penalty branch (mutually exclusive with turn-away).
            WithInput(new[] { Obstacle(6f, -2f) }, input =>
            {
                var cfg = RichConfig();
                var eval = Cost.Evaluate(RichState, U, PrevU, input, cfg, Step);
                var breakdown = Cost.EvaluateBreakdown(RichState, U, PrevU, input, cfg, Step);

                Assert.That(breakdown.collision, Is.GreaterThan(0f), "Fixture must exercise the collision penalty.");
                Assert.That(breakdown.obstacle, Is.EqualTo(0f), "An overlapping hull pays only the penalty, never turn-away.");

                Assert.That(eval, Is.EqualTo(breakdown.total).Within(0.05f).Percent,
                    "The collision branch must sum identically in Evaluate and the breakdown.");
            });
        }
    }
}
#endif

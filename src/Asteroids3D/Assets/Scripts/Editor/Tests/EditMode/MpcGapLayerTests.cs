#if UNITY_EDITOR
using AI.Scanning;
using Game;
using Movement;
using Movement.MPC;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// A3 gap-layer tests: the closed-form egocircle gap detector (axis/width/occlusion/
    /// bank-only classification over scanned circles) and Biased-MPPI-style primitive
    /// injection — a contrived bank-only gap that Gaussian sampling cannot thread but an
    /// injected bank-pulse primitive wins outright.
    /// </summary>
    [Category("MPC")]
    public class MpcGapLayerTests
    {
        // Live DefaultSettings.asset hull/bank values.
        private const float ShipRadius = 1.4f;
        private const float SafetyMargin = 0.1f;
        private const float MaxBankRad = 35f * Mathf.Deg2Rad;
        private const float HullFull = ShipRadius + SafetyMargin;                          // 1.5
        private static readonly float HullBank = ShipRadius * Mathf.Cos(MaxBankRad) + SafetyMargin; // ≈1.247

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            if (!GamePlane.IsConfigured) GamePlane.Configure(PlaneAxis.Y);
        }

        private static ObstacleScan MakeScan(params (float2 pos, float radius)[] obstacles)
        {
            var buffer = new DetectedObstacle[obstacles.Length];
            for (var i = 0; i < obstacles.Length; i++)
            {
                var world = GamePlane.PlanePointToWorld(new Vector2(obstacles[i].pos.x, obstacles[i].pos.y));
                buffer[i] = new DetectedObstacle(world, obstacles[i].radius, null);
            }
            return new ObstacleScan(buffer, buffer.Length);
        }

        private static int FindGapNearAxis(DetectedGap[] gaps, int count, float axis, float tolerance)
        {
            for (var i = 0; i < count; i++)
                if (Mathf.Abs(Cost.WrapRadians(gaps[i].axisAngle - axis)) < tolerance)
                    return i;
            return -1;
        }

        [Test]
        public void Detect_TwoDiscs_YieldsForwardGapWithCorrectAxisAndWidth()
        {
            // Discs r=2 at (±4, 8): forward free interval between their bank-hull blocked
            // arcs is [−0.092, +0.092] rad → axis 0 (upright-free sub-interval midpoint),
            // width ≈ 0.184 rad. (Plus the wide rear gap.)
            var scan = MakeScan((new float2(-4f, 8f), 2f), (new float2(4f, 8f), 2f));
            var gaps = new DetectedGap[16];
            var count = new GapDetector().Detect(float2.zero, scan, HullFull, HullBank, 40f, gaps);

            var fwd = FindGapNearAxis(gaps, count, 0f, 0.05f);
            Assert.That(fwd, Is.GreaterThanOrEqualTo(0), "Expected a gap straight ahead between the discs");
            Assert.That(gaps[fwd].bankOnly, Is.False, "This gap is passable upright");
            Assert.That(gaps[fwd].angularWidth, Is.EqualTo(0.184f).Within(0.03f));
            Assert.That(gaps[fwd].depth, Is.EqualTo(6.94f).Within(0.2f), "Depth = obstacle distance − radius");

            // With the goal straight ahead, the forward gap must outscore the rear gap and win.
            for (var i = 0; i < count; i++)
                gaps[i].score = GapDetector.Score(in gaps[i], 0f, 10f, 0f, 3.14f);
            var chosen = new GapDetector().ChooseGap(gaps, count, 0.25f);
            Assert.That(chosen, Is.EqualTo(fwd), "Goal-aligned gap should be chosen");
        }

        [Test]
        public void Detect_OccludedObstacle_CreatesNoSpuriousGap()
        {
            // The far disc sits entirely inside the near disc's blocked arc: the merged set
            // is identical to the near disc alone — exactly one (rear) gap, none forward.
            var scan = MakeScan((new float2(0f, 10f), 2f), (new float2(0f, 20f), 2f));
            var gaps = new DetectedGap[16];
            var count = new GapDetector().Detect(float2.zero, scan, HullFull, HullBank, 40f, gaps);

            Assert.That(count, Is.EqualTo(1), "Occluded obstacle must not split the free space");
            Assert.That(FindGapNearAxis(gaps, count, 0f, 0.3f), Is.EqualTo(-1),
                "No gap may point at the occluding pair");
            Assert.That(Mathf.Abs(gaps[0].axisAngle), Is.GreaterThan(3f),
                "The single free interval faces away from the blocker");
        }

        [Test]
        public void Detect_NarrowGap_ClassifiedBankOnly()
        {
            // Discs r=2 at (±3.3, 6): the forward corridor is blocked for the upright hull
            // (arcs overlap through 0) but open for the bank-narrowed hull.
            var scan = MakeScan((new float2(-3.3f, 6f), 2f), (new float2(3.3f, 6f), 2f));
            var gaps = new DetectedGap[16];
            var count = new GapDetector().Detect(float2.zero, scan, HullFull, HullBank, 40f, gaps);

            var fwd = FindGapNearAxis(gaps, count, 0f, 0.05f);
            Assert.That(fwd, Is.GreaterThanOrEqualTo(0), "Bank-narrowed hull should see the forward gap");
            Assert.That(gaps[fwd].bankOnly, Is.True,
                "Gap free only under the banked hull must be flagged bank-only");
        }

        [Test]
        public void Synthesize_BankPulse_CoversGapEntryAndUnwinds()
        {
            var dyn = TestDynamics();
            var gap = new DetectedGap { axisAngle = 0f, depth = 10f, angularWidth = 0.1f, bankOnly = true };
            var state = new State { vel = new float2(0f, 20f) }; // yaw 0, already aligned
            const int horizon = 17;
            var buffer = new Control[4 * horizon];

            var written = GapPrimitives.Synthesize(in gap, in state, in dyn, 0.1f, horizon, 4, buffer, 0);
            Assert.That(written, Is.EqualTo(4));

            // entryStep = depth / speed / dt = 10/20/0.1 = 5.
            for (var v = 0; v < written; v++)
            {
                var offset = v * horizon;
                var pulseSteps = 0;
                var strafeSum = 0f;
                for (var j = 0; j < horizon; j++)
                {
                    var u = buffer[offset + j];
                    Assert.That(u.thrust, Is.EqualTo(1f), "Chase primitives hold full thrust");
                    if (Mathf.Abs(u.strafe) > 0.99f) pulseSteps++;
                    strafeSum += u.strafe;
                }
                Assert.That(pulseSteps, Is.GreaterThanOrEqualTo(4),
                    "Pulse + unwind must hold |strafe| = 1 through the traversal");
                Assert.That(Mathf.Abs(strafeSum), Is.LessThan(0.01f),
                    "Unwind pulse must mirror the bank pulse so lateral velocity cancels");
                Assert.That(Mathf.Abs(buffer[offset + 5].strafe), Is.EqualTo(1f).Within(1e-4f),
                    "The pulse window must cover the gap entry step");
            }
        }

        [Test]
        public void Injection_BankOnlyGap_InjectedPrimitiveWinsElite()
        {
            // Wall with a bank-only slot: discs r=2 at (±3.47, 12). Straight through upright
            // collides (3.47 < 3.5); banked clears (3.47 > 3.247). Goal dead ahead past the
            // wall. Gaussian sampling essentially cannot hold |strafe|≈1 through the slot
            // while staying centered — the injected bank-pulse primitive must win the elite.
            var dyn = TestDynamics();
            var cfg = SolverConfig(dyn);
            var scan = MakeScan((new float2(-3.47f, 12f), 2f), (new float2(3.47f, 12f), 2f));
            var state = new State { pos = float2.zero, vel = new float2(0f, 20f), yaw = 0f };
            var goal = new float2(0f, 40f);

            var gap = new DetectedGap { axisAngle = 0f, depth = 10f, angularWidth = 0.05f, bankOnly = true };
            var injected = new Control[4 * cfg.horizon];
            var injectedCount = GapPrimitives.Synthesize(in gap, in state, in dyn, cfg.dt, cfg.horizon, 4, injected, 0);

            // Pin the sampler (Track B's seed seam) so the Gaussian baseline is deterministic:
            // unpinned, the frameCount-derived seed makes the best Gaussian candidate drift
            // across runs and the margin assertion below is a coin flip near the threshold.
            SolverBuffers.SamplerSeedOverride = 1234u;
            try
            {
                var costWithout = SolveOnce(state, cfg, dyn, scan, goal, null, 0, out _);
                var costWith = SolveOnce(state, cfg, dyn, scan, goal, injected, injectedCount, out var bestIndex);

                Assert.That(costWith, Is.LessThan(costWithout - 10f),
                    "Injected bank-through primitive must beat the best Gaussian candidate decisively");
                Assert.That(bestIndex, Is.InRange(1, injectedCount),
                    "The winning candidate must be one of the injected primitives");
            }
            finally
            {
                SolverBuffers.SamplerSeedOverride = null;
            }
        }

        private static float SolveOnce(State state, Config cfg, Dynamics dyn, ObstacleScan scan,
            float2 goal, Control[] injected, int injectedCount, out int bestIndex)
        {
            using var solver = new SolverBuffers();
            var sequence = new Control[cfg.horizon];
            var cost = solver.Solve(state, sequence, cfg, dyn, scan, true,
                goal, default, default, default, float.NaN, 0f, default, 0f,
                samples: 512, noiseStd: 0.75f, noiseKnots: 5, lastControl: default,
                boostCooldownRemaining: 0f, boostSampleProbability: 0f, eliteFraction: 0.113f,
                injectedControls: injected, injectedCount: injectedCount);
            bestIndex = solver.LastBestIndex;
            return cost;
        }

        private static Dynamics TestDynamics() => new(
            mass: 800f, forwardAcc: 5600f, reverseAcc: 2800f,
            maxStrafeAcc: 4000f, minStrafeAcc: 3200f,
            maxSpeed: 25f, maxYawRate: Mathf.PI, yawTorque: 15000f,
            angularDrag: 3f, linearDrag: 0.3f, yawInertia: 800f,
            bankTorque: 20000f, bankDamping: 2000f, maxBankAngleRad: MaxBankRad,
            shipRadius: ShipRadius);

        private static Config SolverConfig(in Dynamics dyn)
        {
            var cfg = new Config
            {
                dt = 0.1f,
                invDt = 10f,
                horizon = 17,
                wPos = 50f,
                positionCurve = 2f,
                positionSaturationDistance = 35f,
                wObstacle = 1f,
                collisionPenalty = 1000f,
                collisionSafetyMargin = SafetyMargin,
                facingTarget = float.NaN,
            };
            cfg.ApplyDynamics(in dyn);
            return cfg;
        }
    }
}
#endif

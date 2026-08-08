#if UNITY_EDITOR
using Movement;
using Movement.MPC;
using NUnit.Framework;
using Ships;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Solver-level tests for the extracted <see cref="Mpc"/> planner. These drive
    /// <c>Mpc.Plan(in MpcInputs)</c> directly — no ship, physics, or decision seam.
    /// Assertions are differential (compare two configurations) so they're robust to the
    /// sampler's stochasticity.
    /// </summary>
    [Category("MPC")]
    public class MpcSolverTests
    {
        private const string MpcSettingsPath = "Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset";
        private const string ShipPrefabPath = "Assets/Prefabs/Ships/Ship_1.prefab";

        private MpcSettings settings;
        private Dynamics dynamics;

        [SetUp]
        public void SetUp()
        {
            settings = AssetDatabase.LoadAssetAtPath<MpcSettings>(MpcSettingsPath);
            var ship = AssetDatabase.LoadAssetAtPath<Ship>(ShipPrefabPath);
            Assert.That(settings, Is.Not.Null, $"Missing MPC settings at {MpcSettingsPath}");
            Assert.That(ship, Is.Not.Null, $"Missing ship prefab at {ShipPrefabPath}");
            dynamics = ship.ResolveStats().Dynamics;
        }

        private static MpcInputs VelocityInputs(float2 velocityReference) => new()
        {
            kinematics = default,                 // ship at rest at origin, yaw 0 (nose +Y)
            velocityReference = velocityReference,
            facingRad = float.NaN,
            enemyYaw = float.NaN,                 // no enemy
            obstacleScan = default,
            enableObstacleAvoidance = false,
        };

        // Warm-start the solver, then average the predicted terminal state over several solves
        // so per-solve sampling noise cancels out.
        private State SolveTerminal(MpcInputs inputs, int warmup = 8, int average = 6)
        {
            using var mpc = new Mpc(settings, dynamics, 0u);
            for (var i = 0; i < warmup; i++) mpc.Plan(in inputs);

            float2 posSum = default;
            float2 velSum = default;
            var yawSum = 0f;
            for (var i = 0; i < average; i++)
            {
                mpc.Plan(in inputs);
                var terminal = mpc.PredictedStates[^1];
                posSum += terminal.pos;
                velSum += terminal.vel;
                yawSum += terminal.yaw;
            }
            return new State { pos = posSum / average, vel = velSum / average, yaw = yawSum / average };
        }

        private Control[] SolveSequence(int seed, MpcInputs inputs, int solves = 6)
        {
            using var mpc = new Mpc(settings, dynamics, (uint)seed);
            for (var i = 0; i < solves; i++) mpc.Plan(in inputs);
            return (Control[])mpc.BestSequence.Clone();
        }

        // Candidate noise is where the injected seed acts. The elite-averaged control converges
        // toward the same near-optimum regardless of seed (good MPC behavior), so divergence is
        // asserted here, at the raw candidate buffer, not on the planned output.
        private Control[] SolveCandidates(int seed, MpcInputs inputs)
        {
            using var mpc = new Mpc(settings, dynamics, (uint)seed);
            mpc.Plan(in inputs);
            var solver = mpc.Solver;
            var count = solver.LastSampleCount * solver.LastHorizon;
            var snapshot = new Control[count];
            for (var i = 0; i < count; i++) snapshot[i] = solver.Candidates[i];
            return snapshot;
        }

        private static bool SequencesEqual(Control[] a, Control[] b)
        {
            if (a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; i++)
                if (a[i].thrust != b[i].thrust || a[i].strafe != b[i].strafe ||
                    a[i].yawTorque != b[i].yawTorque || a[i].boost != b[i].boost)
                    return false;
            return true;
        }

        [Test]
        public void Plan_SameSeedAndInputs_ReplaysIdenticalControls()
        {
            var inputs = VelocityInputs(new float2(6f, 3f));
            var first = SolveSequence(1234, inputs);
            var second = SolveSequence(1234, inputs);

            Assert.That(SequencesEqual(first, second), Is.True,
                "A fixed seed with identical inputs must reproduce the same planned controls bit-for-bit.");
        }

        [Test]
        public void Plan_SameSeed_SamplesIdenticalCandidateNoise()
        {
            var inputs = VelocityInputs(new float2(6f, 3f));
            Assert.That(SequencesEqual(SolveCandidates(1234, inputs), SolveCandidates(1234, inputs)), Is.True,
                "A fixed seed must reproduce the same candidate noise.");
        }

        [Test]
        public void Plan_DifferentSeeds_SampleDifferentCandidateNoise()
        {
            var inputs = VelocityInputs(new float2(6f, 3f));
            Assert.That(SequencesEqual(SolveCandidates(1, inputs), SolveCandidates(2, inputs)), Is.False,
                "Two ships with different seeds must sample different candidate noise.");
        }

        [Test]
        public void Plan_TracksCommandedVelocity()
        {
            const float speed = 6f;
            var terminal = SolveTerminal(VelocityInputs(new float2(0f, speed)));

            Assert.That(terminal.vel.y, Is.GreaterThan(0.2f * speed),
                "Planned trajectory should accelerate toward the commanded velocity");
            Assert.That(terminal.vel.y, Is.GreaterThan(math.abs(terminal.vel.x)),
                "Planned velocity should track the commanded axis, not drift sideways");
            Assert.That(terminal.pos.y, Is.GreaterThan(1f),
                "Tracking the command should carry the ship along the commanded direction");
        }

        [Test]
        public void Plan_FacingOverride_SteersYawTowardRequestedHeading()
        {
            var left = VelocityInputs(float2.zero);
            left.facingRad = 0.5f * Mathf.PI;          // +90°
            var right = VelocityInputs(float2.zero);
            right.facingRad = -0.5f * Mathf.PI;        // -90°

            var yawLeft = SolveTerminal(left).yaw;
            var yawRight = SolveTerminal(right).yaw;

            Assert.That(yawLeft, Is.GreaterThan(yawRight),
                "Opposite facing overrides should drive planned yaw in opposite directions");
        }

        // NOTE: A projectile-lead facing test (enemy moving, projectileSpeed > 0 shifts the
        // planned facing toward the intercept) belongs here, but at base facing weights the
        // shift is sub-degree, so it isn't meaningfully assertable. Revive with an
        // amplified-wFacing settings asset.
    }
}
#endif

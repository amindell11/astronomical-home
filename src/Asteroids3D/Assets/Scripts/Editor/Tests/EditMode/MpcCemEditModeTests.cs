#if UNITY_EDITOR
using System;
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
    /// Iterative-CEM tests (A3 part 1): the per-channel adaptive sigma with a strafe floor, and
    /// convergence of the elite-mean cost across iterations. Drives the real solver via
    /// <see cref="Mpc"/> and reads the CEM seams on <see cref="SolverBuffers"/>.
    /// </summary>
    [Category("MPC")]
    public class MpcCemEditModeTests
    {
        private const string MpcSettingsPath = "Assets/Settings/AI/MPC/MpcSettings.asset";
        private const string ShipSettingsPath = "Assets/Settings/Ships/DefaultSettings.asset";

        private MpcSettings settings;
        private Dynamics dynamics;

        [SetUp]
        public void SetUp()
        {
            settings = AssetDatabase.LoadAssetAtPath<MpcSettings>(MpcSettingsPath);
            var ship = AssetDatabase.LoadAssetAtPath<ShipSettings>(ShipSettingsPath);
            Assert.That(settings, Is.Not.Null, $"Missing MPC settings at {MpcSettingsPath}");
            Assert.That(ship, Is.Not.Null, $"Missing ship settings at {ShipSettingsPath}");
            dynamics = ship.Dynamics;
        }

        private static MpcInputs WaypointInputs(float2 goalPos) => new()
        {
            kinematics = default,
            goalPos = goalPos,
            goalMode = GoalMode.Waypoint,
            facingRad = float.NaN,
            enemyYaw = float.NaN,
            weightOverrides = Array.Empty<WeightOverride>(),
            obstacleScan = default,
            enableObstacleAvoidance = false,
        };

        [Test]
        public void FloorSigma_ClampsPerChannel()
        {
            var floored = SolverBuffers.FloorSigma(float3.zero, 0.3f, 0.05f);
            Assert.That(floored.x, Is.EqualTo(0.05f), "thrust sigma floored to sigmaFloor");
            Assert.That(floored.y, Is.EqualTo(0.3f), "strafe sigma floored to strafeSigmaFloor");
            Assert.That(floored.z, Is.EqualTo(0.05f), "yaw sigma floored to sigmaFloor");

            // Values above the floor pass through untouched.
            var big = SolverBuffers.FloorSigma(new float3(0.6f, 0.7f, 0.8f), 0.3f, 0.05f);
            Assert.That(big.x, Is.EqualTo(0.6f));
            Assert.That(big.y, Is.EqualTo(0.7f));
            Assert.That(big.z, Is.EqualTo(0.8f));
        }

        [Test]
        public void AdaptiveSigma_StrafeChannel_NeverCollapsesBelowFloor()
        {
            // A waypoint dead ahead makes the optimal strafe ≈ 0, so CEM elites converge on the
            // strafe channel and its raw variance shrinks toward 0 — the floor must hold it up.
            using var mpc = new Mpc(settings, dynamics);
            var inputs = WaypointInputs(new float2(30f, 0f));
            for (var i = 0; i < 10; i++) mpc.Plan(in inputs);

            var sigma = mpc.Solver.LastSigma;
            Assert.That(sigma.y, Is.GreaterThanOrEqualTo(settings.strafeSigmaFloor - 1e-4f),
                $"Strafe sampling sigma must stay ≥ strafeSigmaFloor; got {sigma.y}");
            Assert.That(sigma.x, Is.GreaterThanOrEqualTo(settings.sigmaFloor - 1e-4f),
                "Thrust sigma must stay ≥ sigmaFloor");
            Assert.That(sigma.z, Is.GreaterThanOrEqualTo(settings.sigmaFloor - 1e-4f),
                "Yaw sigma must stay ≥ sigmaFloor");
        }

        [Test]
        public void CemIterations_EliteMeanCost_DoesNotIncrease()
        {
            using var mpc = new Mpc(settings, dynamics);
            var inputs = WaypointInputs(new float2(30f, 0f));
            // Warm up so the warm-start mean is a sensible starting point, then inspect the last solve.
            for (var i = 0; i < 8; i++) mpc.Plan(in inputs);
            mpc.Plan(in inputs);

            var iterCosts = mpc.Solver.LastIterationCosts;
            Assert.That(iterCosts, Is.Not.Null);
            Assert.That(iterCosts.Length, Is.EqualTo(settings.cemIterations),
                "One elite-mean cost recorded per CEM iteration");

            // Refining the mean each iteration must not make the elite-mean cost worse overall.
            var tol = 1e-3f * (1f + math.abs(iterCosts[0]));
            Assert.That(iterCosts[^1], Is.LessThanOrEqualTo(iterCosts[0] + tol),
                $"Final CEM iteration should be no worse than the first: [{string.Join(", ", iterCosts)}]");
        }
    }
}
#endif

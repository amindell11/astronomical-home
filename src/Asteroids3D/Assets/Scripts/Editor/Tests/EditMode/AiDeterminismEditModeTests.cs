#if UNITY_EDITOR
using AI;
using AI.Context;
using AI.States;
using Movement;
using Movement.MPC;
using NUnit.Framework;
using Ships;
using Ships.Command;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Determinism substrate for the RL/self-play phase: the strategy-layer RNG is a seeded
    /// per-ship <see cref="System.Random"/> injected through the Initialize wiring, so a fixed
    /// seed reproduces the same patrol waypoints and distinct seeds diverge. The MPC sampler's
    /// counterpart lives in <see cref="MpcSolverTests"/>.
    /// </summary>
    [Category("AI")]
    public class AiDeterminismEditModeTests
    {
        private sealed class ShipStatusStub : IShipStatus
        {
            public ShipId Id => ShipId.Invalid;
            public Transform Transform => null;
            public Kinematics Kinematics { get; }
            public Dynamics Dynamics => default;
            public float HealthPct => 1f;
            public float ShieldPct => 1f;
            public bool BoostAvailable => false;
            public float BoostCooldownRemaining => 0f;
            public float MaxSpeed => 0f;
            public float MaxYawRate => 0f;

            public ShipStatusStub(Vector2 pos) =>
                Kinematics = new Kinematics(pos, Vector2.zero, 0f, 0f, 0f);
        }

        private GameObject navigatorObject;
        private GameObject scoutObject;

        [SetUp]
        public void SetUp()
        {
            navigatorObject = new GameObject("Navigator");
            scoutObject = new GameObject("Scout");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(navigatorObject);
            Object.DestroyImmediate(scoutObject);
        }

        private Vector2 FirstPatrolWaypoint(int seed)
        {
            var navigator = navigatorObject.AddComponent<Navigator>();
            var scout = scoutObject.AddComponent<Scout>();
            try
            {
                var context = new AIContext(new ShipStatusStub(new Vector2(5f, -3f)), scout);
                var goal = new RandomWaypointGoal { patrolRadius = 50f, minDistanceFactor = 0.3f };
                var runner = GoalRunner.Create(goal, navigator, seed);
                runner.Enter(context);
                return navigator.CurrentWaypoint.position;
            }
            finally
            {
                Object.DestroyImmediate(navigator);
                Object.DestroyImmediate(scout);
            }
        }

        [Test]
        public void Patrol_SameSeed_PicksIdenticalWaypoint()
        {
            Assert.That(FirstPatrolWaypoint(777), Is.EqualTo(FirstPatrolWaypoint(777)),
                "A fixed seed must reproduce the same patrol waypoint.");
        }

        [Test]
        public void Patrol_DifferentSeeds_PickDifferentWaypoints()
        {
            Assert.That(FirstPatrolWaypoint(1), Is.Not.EqualTo(FirstPatrolWaypoint(2)),
                "Distinct per-ship seeds must produce distinct patrol waypoints.");
        }
    }
}
#endif

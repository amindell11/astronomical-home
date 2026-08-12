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
    /// <summary>Validates the VelocityReference objective: closed-form unit tests of <see cref="Cost.VelocityTrackCost"/>, plus a tracking-fidelity test driving <c>Mpc.Plan</c> directly to confirm the solver converges velocity onto the command — tight on-axis, within a characterized strafe-authority envelope off-axis.</summary>
    [Category("MPC")]
    public class MpcVelocityReferenceEditModeTests
    {
        [Test]
        public void VelocityTrackCost_ZeroAtReference()
        {
            var v = new float2(6f, -3f);
            Assert.That(Cost.VelocityTrackCost(v, v, 100f), Is.EqualTo(0f));
        }

        [Test]
        public void VelocityTrackCost_IsNormalizedSquaredError()
        {
            // ‖(0,0) − (3,4)‖² / 25 = 25 / 25 = 1.
            Assert.That(Cost.VelocityTrackCost(float2.zero, new float2(3f, 4f), 25f),
                Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void VelocityTrackCost_MonotonicAndSymmetricInError()
        {
            var vRef = new float2(5f, 0f);
            var near = Cost.VelocityTrackCost(new float2(4f, 0f), vRef, 100f);
            var far = Cost.VelocityTrackCost(new float2(1f, 0f), vRef, 100f);
            Assert.That(far, Is.GreaterThan(near), "Cost must grow with tracking error.");

            var plus = Cost.VelocityTrackCost(vRef + new float2(0f, 2f), vRef, 100f);
            var minus = Cost.VelocityTrackCost(vRef - new float2(0f, 2f), vRef, 100f);
            Assert.That(plus, Is.EqualTo(minus).Within(1e-5f), "Error is symmetric about the reference.");
        }

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

        // Nose pinned by facingRad so the off-axis case genuinely tests strafe authority, not a yaw-then-thrust; the closed-loop driver overwrites kinematics each step.
        private MpcInputs VelocityInputs(float2 vRef, float facingRad) => new()
        {
            kinematics = default,
            velocityReference = vRef,
            facingRad = facingRad,
            enemyYaw = float.NaN,
            obstacleScan = default,
            enableObstacleAvoidance = false,
        };

        // Receding-horizon controller in miniature: re-plan each step and advance the same Model the solver rolls out on. A single plan from rest only sees one horizon of acceleration and drastically under-measures tracking.
        private float2 ClosedLoopFinalVelocity(float2 vRef, float facingRad, float2 initialVel = default, int steps = 100)
        {
            using var mpc = new Mpc(settings, dynamics, 0u);
            var cfg = settings.ToConfig(facingRad);
            cfg.ApplyDynamics(in dynamics);

            var state = new State { vel = initialVel }; // origin, nose +Y (yaw 0)
            for (var i = 0; i < steps; i++)
            {
                var inputs = VelocityInputs(vRef, facingRad);
                inputs.kinematics = new Kinematics(
                    new Vector2(state.pos.x, state.pos.y), new Vector2(state.vel.x, state.vel.y),
                    state.yaw * Mathf.Rad2Deg, state.yawRate * Mathf.Rad2Deg, 0f);

                var r = mpc.Plan(in inputs);
                var u = new Control { thrust = r.thrust, strafe = r.strafe, yawTorque = r.yawTorque };
                state = Model.Step(state, u, cfg, dynamics);
            }
            return state.vel;
        }

        [Test]
        public void OnAxis_ConvergesTowardCommandedForwardVelocity()
        {
            var speed = 0.5f * dynamics.maxSpeed;
            var vel = ClosedLoopFinalVelocity(new float2(0f, speed), facingRad: 0f);

            Assert.That(vel.y, Is.GreaterThan(0.3f * speed),
                "Closed-loop, a forward command must settle meaningful forward velocity toward the reference.");
            Assert.That(vel.y, Is.GreaterThan(math.abs(vel.x)),
                "Forward tracking must dominate any lateral drift (the commanded axis wins).");
        }

        [Test]
        public void OffAxis_StrafesTowardCommand_ForwardAuthorityDominates()
        {
            var speed = 0.5f * dynamics.maxSpeed;
            var forward = ClosedLoopFinalVelocity(new float2(0f, speed), facingRad: 0f);
            var strafe = ClosedLoopFinalVelocity(new float2(speed, 0f), facingRad: 0f);

            Assert.That(strafe.x, Is.GreaterThan(0f),
                "A perpendicular command (nose pinned) must still push strafe velocity toward it.");
            Assert.That(forward.y, Is.GreaterThan(strafe.x),
                "Same-magnitude command: forward authority must strictly exceed strafe authority — " +
                "moving hard perpendicular to the nose is the physically-hardest case. This is the " +
                "characterized envelope, not a tight tracking bound (strafe authority is limited at " +
                "the base wVelTrack; the reward tunes it in PR-3).");
        }

        [Test]
        public void ZeroReference_BrakesAMovingShip()
        {
            var v0 = 0.5f * dynamics.maxSpeed;
            var vel = ClosedLoopFinalVelocity(float2.zero, facingRad: 0f, initialVel: new float2(0f, v0));

            Assert.That(math.length(vel), Is.LessThan(0.5f * v0),
                "A zero reference is a valid 'stop' command — the loop must brake the ship toward rest.");
        }
    }
}
#endif

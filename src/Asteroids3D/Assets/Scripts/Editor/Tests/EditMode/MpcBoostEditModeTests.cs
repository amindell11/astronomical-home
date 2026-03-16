using NUnit.Framework;
using Movement;
using Movement.MPC;
using Unity.Mathematics;

namespace Tests.EditMode
{
    [Category("MPC")]
    public class MpcBoostEditModeTests
    {
        private static Dynamics MakeBoostDynamics(float boostImpulse = 14000f, float boostCooldown = 3f)
        {
            return new Dynamics(
                mass: 1000f, forwardAcc: 7000f, reverseAcc: 3500f,
                maxStrafeAcc: 5000f, minStrafeAcc: 4000f,
                maxSpeed: 25f, maxYawRate: 3.14f,
                yawTorque: 7000f, angularDrag: 1.7f, linearDrag: 0.5f,
                yawInertia: 1000f,
                boostImpulse: boostImpulse, boostCooldown: boostCooldown);
        }

        private static Config MakeConfig(float dt = 0.1f)
        {
            return new Config { dt = dt, invDt = 1f / dt, horizon = 15 };
        }

        [Test]
        public void Step_BoostAppliesForwardImpulse_WhenCooldownReady()
        {
            var dyn = MakeBoostDynamics();
            var cfg = MakeConfig();
            var state = new State { pos = float2.zero, vel = float2.zero, yaw = 0f };
            var control = new Control { boost = 1f };

            var next = Model.Step(state, control, cfg, dyn);

            // Boost impulse = 14000 / 1000 = 14 m/s along forward (yaw=0 → +Y)
            // Clamped to maxSpeed 25
            Assert.Greater(next.vel.y, 0f, "Boost should add forward velocity");
            Assert.AreEqual(14f, next.vel.y, 0.5f);
        }

        [Test]
        public void Step_BoostSetsCooldown()
        {
            var dyn = MakeBoostDynamics();
            var cfg = MakeConfig();
            var state = new State { pos = float2.zero, vel = float2.zero, yaw = 0f };
            var control = new Control { boost = 1f };

            var next = Model.Step(state, control, cfg, dyn);

            Assert.AreEqual(dyn.boostCooldown, next.boostCooldownRemaining, 0.01f);
        }

        [Test]
        public void Step_BoostIgnored_WhenOnCooldown()
        {
            var dyn = MakeBoostDynamics();
            var cfg = MakeConfig();
            var state = new State
            {
                pos = float2.zero, vel = float2.zero, yaw = 0f,
                boostCooldownRemaining = 2f
            };
            var control = new Control { boost = 1f };

            var next = Model.Step(state, control, cfg, dyn);

            // Velocity should be near zero (only drag/thrust effects, no boost)
            Assert.AreEqual(0f, next.vel.y, 0.01f, "Boost should not fire during cooldown");
            // Cooldown should tick down by dt
            Assert.AreEqual(1.9f, next.boostCooldownRemaining, 0.01f);
        }

        [Test]
        public void Step_CooldownTicksToZero()
        {
            var dyn = MakeBoostDynamics();
            var cfg = MakeConfig();
            var state = new State
            {
                pos = float2.zero, vel = float2.zero, yaw = 0f,
                boostCooldownRemaining = 0.05f
            };
            var control = new Control { boost = 0f };

            var next = Model.Step(state, control, cfg, dyn);

            Assert.AreEqual(0f, next.boostCooldownRemaining, "Cooldown should clamp to zero");
        }

        [Test]
        public void Step_BoostNotApplied_WhenControlBelowThreshold()
        {
            var dyn = MakeBoostDynamics();
            var cfg = MakeConfig();
            var state = new State { pos = float2.zero, vel = float2.zero, yaw = 0f };
            var control = new Control { boost = 0.3f };

            var next = Model.Step(state, control, cfg, dyn);

            Assert.AreEqual(0f, next.vel.y, 0.01f, "Boost below 0.5 threshold should not fire");
        }

        [Test]
        public void Step_BoostClampsToMaxSpeed()
        {
            // Ship already at 20 m/s, boost adds 14 m/s → should clamp to 25
            var dyn = MakeBoostDynamics();
            var cfg = MakeConfig();
            var state = new State
            {
                pos = float2.zero, vel = new float2(0f, 20f), yaw = 0f
            };
            var control = new Control { boost = 1f };

            var next = Model.Step(state, control, cfg, dyn);

            Assert.LessOrEqual(math.length(next.vel), dyn.maxSpeed + 0.01f);
        }

        [Test]
        public void Step_NoCooldownDoubleBoost_WithinHorizon()
        {
            var dyn = MakeBoostDynamics(boostCooldown: 3f);
            var cfg = MakeConfig(dt: 0.1f);
            var state = new State { pos = float2.zero, vel = float2.zero, yaw = 0f };
            var boostControl = new Control { boost = 1f };

            // First boost fires
            var s1 = Model.Step(state, boostControl, cfg, dyn);
            Assert.Greater(s1.vel.y, 0f);
            Assert.Greater(s1.boostCooldownRemaining, 0f);

            // Second boost in next step should NOT fire (cooldown active)
            var velBeforeSecond = s1.vel.y;
            var s2 = Model.Step(s1, boostControl, cfg, dyn);
            // Velocity should not increase by another boost impulse
            // (it may decrease slightly due to drag)
            Assert.LessOrEqual(s2.vel.y, velBeforeSecond + 0.1f,
                "Second boost should be blocked by cooldown");
        }
    }
}

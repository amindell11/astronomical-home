#if UNITY_EDITOR
using System.Collections.Generic;
using AI;
using AI.Navigation.Aim;
using AI.Navigation.MPC;
using Combat.Targeting;
using Combat.Weapons;
using Movement;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Tests.Common;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Pins the aim servo's law against the MPC's own yaw model, the yaw handover rule, and the Gunner's aim reference.</summary>
    [Category("AI")]
    public class AimServoEditModeTests
    {
        private const string ShipPrefabPath = "Assets/Prefabs/Ships/Crimson.prefab";
        private const string MpcSettingsPath = "Assets/Settings/AI/MPC/MpcSettings_AgentPilot.asset";
        private const float Dt = 0.02f;

        private MpcSettings settings;
        private Dynamics dynamics;
        private Config cfg;

        [SetUp]
        public void SetUp()
        {
            settings = AssetDatabase.LoadAssetAtPath<MpcSettings>(MpcSettingsPath);
            Assert.That(settings, Is.Not.Null, $"Missing MPC settings at {MpcSettingsPath}");
            var ship = AssetDatabase.LoadAssetAtPath<Ship>(ShipPrefabPath);
            Assert.That(ship, Is.Not.Null, $"Missing ship prefab at {ShipPrefabPath}");
            dynamics = ship.ResolveStats().Dynamics;
            cfg = settings.ToConfig();
            cfg.dt = Dt;
            cfg.ApplyDynamics(in dynamics);
        }

        private static Kinematics Pose(in State s) =>
            new(s.pos, s.vel, s.yaw * Mathf.Rad2Deg, s.yawRate * Mathf.Rad2Deg, 0f);

        private float Torque(float yawDeg, float yawRateDeg, float headingDeg, float rateDeg = 0f) =>
            AimServo.YawTorque(new Kinematics(Vector2.zero, Vector2.zero, yawDeg, yawRateDeg, 0f),
                new AimReference(headingDeg * Mathf.Deg2Rad, rateDeg * Mathf.Deg2Rad), in dynamics, settings);

        private State Step(State s, in AimReference reference)
        {
            var torque = AimServo.YawTorque(Pose(s), in reference, in dynamics, settings);
            return Model.Step(s, new Control { yawTorque = torque }, cfg, dynamics);
        }

        private static float ErrorDeg(in State s, float headingRad) =>
            Mathf.Abs(Mathf.DeltaAngle(s.yaw * Mathf.Rad2Deg, headingRad * Mathf.Rad2Deg));

        [Test]
        public void Torque_TurnsTowardTheHeading_EitherSide_AndAcrossTheWrap()
        {
            Assert.Greater(Torque(0f, 0f, 30f), 0f, "a heading CCW of the nose torques CCW");
            Assert.Less(Torque(0f, 0f, -30f), 0f, "a heading CW of the nose torques CW");
            Assert.Greater(Torque(170f, 0f, -170f), 0f, "the short way from 170° to -170° is CCW through 180°");
            Assert.Less(Torque(-170f, 0f, 170f), 0f, "the short way from -170° to 170° is CW through 180°");
        }

        [Test]
        public void Torque_SaturatesFarFromTheHeading_AndStaysWithinTheEngine()
        {
            Assert.AreEqual(1f, Torque(0f, 0f, 120f), 1e-6f);
            Assert.AreEqual(-1f, Torque(0f, 0f, -120f), 1e-6f);
            Assert.AreEqual(1f, Torque(0f, -180f, 0f), 1e-6f, "braking a fast spin saturates too");
        }

        [Test]
        public void ClosedLoop_SettlesOnAFixedHeading_WithoutOvershoot()
        {
            var heading = 90f * Mathf.Deg2Rad;
            var reference = new AimReference(heading, 0f);
            var s = new State();
            var overshootDeg = 0f;
            for (var i = 0; i < 75; i++)
            {
                s = Step(s, in reference);
                overshootDeg = Mathf.Max(overshootDeg, (s.yaw - heading) * Mathf.Rad2Deg);
            }

            Assert.Less(ErrorDeg(s, heading), 1f, "1.5 s settles a 90° turn");
            Assert.Less(Mathf.Abs(s.yawRate * Mathf.Rad2Deg), 5f, "and holds it, not swinging through");
            Assert.Less(overshootDeg, 3f);
        }

        [Test]
        public void RateFeedforward_TracksARotatingHeading_WithNoStandingLag()
        {
            var rate = 60f * Mathf.Deg2Rad;
            var s = new State();
            for (var i = 0; i < 75; i++)
                s = Step(s, new AimReference(rate * i * Dt, rate));

            var heading = rate * 75 * Dt;
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(s.yaw * Mathf.Rad2Deg, heading * Mathf.Rad2Deg)), 1f,
                "the bearing rate is fed forward, so the nose rides the heading instead of trailing it");
            Assert.AreEqual(dynamics.yawInertia * dynamics.angularDrag * rate / dynamics.yawTorque,
                Torque(0f, 60f, 0f, 60f), 1e-5f, "on the heading at its rate, only drag is left to cancel");
        }

        [Test]
        public void Handover_TakesYawAtTheEngageWeight_AndReturnsItBelowTheReleaseWeight()
        {
            var handover = new YawHandover(settings);
            var live = new AimReference(0f, 0f);

            handover.UpdateAuthority(0.89f);
            Assert.IsFalse(handover.ServoOwnsYaw(in live), "below the engage weight the MPC keeps yaw");
            handover.UpdateAuthority(0.9f);
            Assert.IsTrue(handover.ServoOwnsYaw(in live), "the engage weight takes yaw");
            handover.UpdateAuthority(0.81f);
            Assert.IsTrue(handover.ServoOwnsYaw(in live), "hysteresis: between the weights the servo keeps yaw");
            handover.UpdateAuthority(0.79f);
            Assert.IsFalse(handover.ServoOwnsYaw(in live), "below the release weight yaw returns");
            handover.UpdateAuthority(0.85f);
            Assert.IsFalse(handover.ServoOwnsYaw(in live), "hysteresis: between the weights the MPC keeps yaw");
            handover.UpdateAuthority(1f);
            Assert.IsFalse(handover.ServoOwnsYaw(AimReference.None), "no reference, no servo");
        }

        [Test]
        public void Handover_ObstacleThreatVetoesTheServo_UntilItHasStayedClearForTheHold()
        {
            var handover = new YawHandover(settings);
            var live = new AimReference(0f, 0f);
            handover.UpdateAuthority(1f);

            handover.ObserveThreat(true, Dt);
            Assert.IsFalse(handover.ServoOwnsYaw(in live), "the MPC keeps yaw while its obstacle handling fires");

            var clearSteps = Mathf.RoundToInt(settings.aimServoVetoHold / Dt);
            for (var i = 0; i < clearSteps / 2; i++) handover.ObserveThreat(false, Dt);
            Assert.IsFalse(handover.ServoOwnsYaw(in live), "and holds it partway through the veto hold");

            for (var i = 0; i < clearSteps; i++) handover.ObserveThreat(false, Dt);
            Assert.IsTrue(handover.ServoOwnsYaw(in live), "past the hold the servo takes yaw back");
        }

        [Test]
        public void AimReference_IsNone_WithoutATarget_AndFollowsTheInterceptBearing_WithOne()
        {
            var host = new GameObject("Gunner");
            try
            {
                var self = new Kinematics(new Vector2(1f, -2f), new Vector2(0f, 4f), 20f, 0f, 0f);
                var weapons = new StubWeaponContext();
                var gunner = host.AddComponent<Gunner>();
                gunner.Initialize(weapons, null, () => self);
                Assert.IsFalse(gunner.PrimaryAimReference().live, "no target, no reference");

                var targetPos = new Vector2(12f, 9f);
                var targetVel = new Vector2(-3f, 2f);
                gunner.Aim(targetPos, targetVel);
                var reference = gunner.PrimaryAimReference();
                var toIntercept = TargetingMath.PredictIntercept(in self, targetPos, targetVel,
                    weapons.ProjectileSpeed(WeaponSlot.Primary)) - self.pos;

                Assert.IsTrue(reference.live);
                Assert.AreEqual(Mathf.Atan2(-toIntercept.x, toIntercept.y), reference.heading, 1e-5f,
                    "the heading is the bearing to the lead the Gunner fires with");

                gunner.HoldFire();
                Assert.IsFalse(gunner.PrimaryAimReference().live, "holding fire drops the reference");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AimReference_Rate_IsTheBearingRate_SignedCcw()
        {
            var host = new GameObject("Gunner");
            try
            {
                var gunner = host.AddComponent<Gunner>();
                gunner.Initialize(new HitscanContext(), null,
                    () => new Kinematics(Vector2.zero, Vector2.zero, 0f, 0f, 0f));
                gunner.Aim(new Vector2(0f, 10f), new Vector2(-5f, 0f));

                var reference = gunner.PrimaryAimReference();
                Assert.AreEqual(0f, reference.heading, 1e-6f, "hitscan aims at the present position");
                Assert.AreEqual(0.5f, reference.rate, 1e-6f, "a target crossing right-to-left at 5 u/s, 10 u out");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private sealed class HitscanContext : IWeaponContext
        {
            private static readonly WeaponSlot[] slots = { WeaponSlot.Primary };
            public IReadOnlyList<WeaponSlot> Slots => slots;
            public bool IsReady(WeaponSlot slot) => true;
            public float ProjectileSpeed(WeaponSlot slot) => 0f;
            public Gunsight Sight(WeaponSlot slot) => null;
        }
    }
}
#endif

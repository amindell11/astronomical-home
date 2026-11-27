using System.Collections;
using Combat.Targeting;
using Loot;
using Movement;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Ships.Keystones;
using Ships.Keystones.Kits;
using Ships.Mods;
using Substrate;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Keystones
{
    [Category("Ships")]
    public class OverdrivePlayModeTests : PlayModeWorldFixture
    {
        private const string ValisPath = "Assets/Prefabs/Ships/Valis.prefab";
        private const string KitPath = "Assets/Prefabs/Keystones/Valis_Overdrive.prefab";
        private const string OrbitPath = "Assets/Settings/Ships/Keystones/Mods/Orbit.asset";
        private const float RockRadius = 4f;
        private const float Speed = 30f;
        private Ship valis;
        private GameObject kitHost;
        private GameObject rock;

        public override void TearDown()
        {
            ShipTestFactory.DestroyShip(valis);
            if (kitHost) Object.DestroyImmediate(kitHost);
            if (rock) Object.DestroyImmediate(rock);
            base.TearDown();
        }

        public enum Pass { Orbiting, NoTurn, TurningAway, NotHeld }

        // The kit flies a point ship past a rock abeam on its left, nose held a little inside the course.
        [Test]
        public void Orbit_BendsTheCourseAroundASkimmedRock_KeepingSpeed_OnlyWhileTurningTowardIt([Values] Pass pass)
        {
            rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.layer = LayerIds.Asteroid;
            rock.transform.position = GamePlane.PlanePointToWorld(Vector2.zero);
            rock.transform.localScale = Vector3.one * (RockRadius * 2f);
            kitHost = Object.Instantiate(TestAssets.Load<GameObject>(KitPath));
            var kit = kitHost.GetComponent<Overdrive>();
            var body = new GameObject("Body").AddComponent<Rigidbody>();
            body.transform.SetParent(kitHost.transform);
            kit.Initialize(new KeystoneHost(null, body, 1f, new LockChannel()));
            if (pass != Pass.NotHeld) kit.Behaviours.Set(new BehaviourMod[] { TestAssets.Load<KeystoneMod>(OrbitPath) });
            Physics.SyncTransforms();
            var turn = pass switch { Pass.NoTurn => 0f, Pass.TurningAway => -1f, _ => 1f };

            var dt = Time.fixedDeltaTime;
            var pos = new Vector2(0f, -(RockRadius + 2f));
            var vel = new Vector2(Speed, 0f);
            var closest = float.PositiveInfinity;
            for (var i = 0; i < Mathf.RoundToInt(0.5f / dt); i++)
            {
                var nose = (Vector2)(Quaternion.AngleAxis(20f, Vector3.forward) * vel.normalized);
                var yaw = Mathf.Atan2(-nose.x, nose.y) * Mathf.Rad2Deg;
                var kinematics = new Kinematics(pos, vel, yaw, 0f, 0f);
                vel += kit.Step(in kinematics, i == 0 ? 1f : 0f, dt, out _, Speed, null, turn).Acceleration * dt;
                pos += vel * dt;
                closest = Mathf.Min(closest, pos.magnitude);
            }

            Assert.That(kit.Active, Is.True, "the pass stays inside the window");
            Assert.That(vel.magnitude, Is.EqualTo(Speed).Within(0.01f), "no speed is lost");
            var bend = Vector2.Angle(Vector2.right, vel);
            if (pass != Pass.Orbiting)
            {
                Assert.That(bend, Is.LessThan(0.01f), "the course runs straight on");
                return;
            }
            Assert.That(bend, Is.GreaterThan(90f), "the course bends around the rock");
            Assert.That(closest, Is.GreaterThan(RockRadius + 1f), "skimming it, never into it");
        }

        [UnityTest]
        public IEnumerator Overdrive_FliesPastTheBaseCap_ThenBleedsBackRatherThanSnapping()
        {
            valis = Object.Instantiate(TestAssets.Load<Ship>(ValisPath), Vector3.zero, GamePlane.Rotation);
            valis.Initialize(0, 0, Projectiles);
            var overdrive = (Overdrive)valis.Keystone;
            var baseCap = valis.Stats.maxSpeed;
            var full = new PilotCommand { thrust = 1f };

            for (var i = 0; i < 200; i++)
            {
                valis.Movement.Drive(full);
                yield return new WaitForFixedUpdate();
            }
            Assert.That(valis.Kinematics.Speed, Is.LessThanOrEqualTo(baseCap + 0.01f), "the base cap holds before overdrive");

            valis.Movement.Drive(new PilotCommand { thrust = 1f, boost = 1f });
            yield return new WaitForFixedUpdate();
            Assert.That(overdrive.Active, Is.True);
            Assert.That(valis.Dynamics.maxSpeed, Is.EqualTo(baseCap * overdrive.PeakSpeedScale).Within(0.01f), "the live model carries the raised cap");

            var peak = 0f;
            while (overdrive.Active)
            {
                valis.Movement.Drive(full);
                yield return new WaitForFixedUpdate();
                peak = Mathf.Max(peak, valis.Kinematics.Speed);
            }
            Assert.That(peak, Is.GreaterThan(baseCap * 1.15f), "overdrive flies well past the base cap");
            Assert.That(overdrive.PaidActivations, Is.EqualTo(1));
            Assert.That(valis.Dynamics.maxSpeed, Is.EqualTo(baseCap).Within(0.01f), "the model returns to base when the window closes");

            for (var i = 0; i < 10; i++)
            {
                valis.Movement.Drive(full);
                yield return new WaitForFixedUpdate();
            }
            Assert.That(valis.Kinematics.Speed, Is.GreaterThan(baseCap + 1f), "0.2 s after the window the speed is still bleeding, not snapped");

            for (var i = 0; i < 150; i++)
            {
                valis.Movement.Drive(full);
                yield return new WaitForFixedUpdate();
            }
            Assert.That(valis.Kinematics.Speed, Is.LessThanOrEqualTo(baseCap + 0.05f), "and settles back at the base cap");
        }

        [UnityTest]
        public IEnumerator Overdrive_RaisesTurningAndStrafe_ThenRestoresThem()
        {
            valis = Object.Instantiate(TestAssets.Load<Ship>(ValisPath), Vector3.zero, GamePlane.Rotation);
            valis.Initialize(0, 0, Projectiles);
            yield return new WaitForFixedUpdate();
            var normal = valis.Dynamics;
            valis.Movement.Drive(new PilotCommand { strafe = 1f, yawTorque = 1f, boost = 1f });
            yield return new WaitForFixedUpdate();

            Assert.That(valis.Keystone.Active, Is.True);
            Assert.That(valis.Movement.dbgStrafe.magnitude, Is.EqualTo(normal.maxStrafeAcc * 1.5f).Within(.01f));
            Assert.That(valis.Movement.dbgYaw, Is.EqualTo(normal.yawTorque * 1.5f).Within(.01f));
            Assert.That(valis.Dynamics.maxStrafeAcc, Is.EqualTo(normal.maxStrafeAcc * 1.5f));
            Assert.That(valis.Dynamics.minStrafeAcc, Is.EqualTo(normal.minStrafeAcc * 1.5f));
            Assert.That(valis.Dynamics.yawTorque, Is.EqualTo(normal.yawTorque * 1.5f));
            Assert.That(valis.Dynamics.maxYawRate, Is.EqualTo(normal.maxYawRate * 1.5f));
            Assert.That(valis.Rigidbody.maxAngularVelocity, Is.EqualTo(valis.Dynamics.maxYawRate).Within(.001f));
            Assert.That(valis.MaxYawRate * Mathf.Deg2Rad, Is.EqualTo(valis.Dynamics.maxYawRate).Within(.001f));

            while (valis.Keystone.Active) yield return new WaitForFixedUpdate();

            Assert.That(valis.Movement.dbgYaw, Is.EqualTo(normal.yawTorque).Within(.01f));
            Assert.That(valis.Dynamics.maxStrafeAcc, Is.EqualTo(normal.maxStrafeAcc));
            Assert.That(valis.Dynamics.minStrafeAcc, Is.EqualTo(normal.minStrafeAcc));
            Assert.That(valis.Dynamics.yawTorque, Is.EqualTo(normal.yawTorque));
            Assert.That(valis.Dynamics.maxYawRate, Is.EqualTo(normal.maxYawRate));
            Assert.That(valis.Rigidbody.maxAngularVelocity, Is.EqualTo(normal.maxYawRate).Within(.001f));
            Assert.That(valis.MaxYawRate * Mathf.Deg2Rad, Is.EqualTo(normal.maxYawRate).Within(.001f));
        }

        [UnityTest]
        public IEnumerator ResetDuringOverdrive_RestoresTheBaseCaps()
        {
            valis = Object.Instantiate(TestAssets.Load<Ship>(ValisPath), Vector3.zero, GamePlane.Rotation);
            valis.Initialize(0, 0, Projectiles);
            yield return new WaitForFixedUpdate();
            valis.Movement.Drive(new PilotCommand { thrust = 1f, boost = 1f });
            yield return new WaitForFixedUpdate();
            Assert.That(valis.Rigidbody.maxLinearVelocity, Is.GreaterThan(valis.Stats.maxSpeed), "overdrive raised the cap");
            Assert.That(valis.Rigidbody.maxAngularVelocity, Is.GreaterThan(valis.Stats.maxYawRate * Mathf.Deg2Rad));

            valis.ResetShip();

            Assert.That(valis.Rigidbody.maxLinearVelocity, Is.EqualTo(valis.Stats.maxSpeed), "an in-place respawn starts at the base cap");
            Assert.That(valis.Rigidbody.maxAngularVelocity, Is.EqualTo(valis.Stats.maxYawRate * Mathf.Deg2Rad));
            Assert.That(valis.MaxYawRate, Is.EqualTo(valis.Stats.maxYawRate));
            Assert.That(valis.Dynamics.yawTorque, Is.EqualTo(valis.Stats.yawTorque));
            Assert.That(valis.Dynamics.maxStrafeAcc, Is.EqualTo(valis.Stats.maxStrafeForce));
        }
    }
}

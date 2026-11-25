using System.Collections;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Ships.Keystones;
using Ships.Keystones.Kits;
using Substrate;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Keystones
{
    [Category("Ships")]
    public class KeystoneDashPlayModeTests : PlayModeWorldFixture
    {
        private Ship ship;

        public override void TearDown()
        {
            ShipTestFactory.DestroyShip(ship);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Dash_AppliesItsImpulseAlongTheNose_OncePerCooldown_AndRespawnReadiesIt()
        {
            ship = Object.Instantiate(TestAssets.Load<Ship>("Assets/Prefabs/Ships/Cargo Ships/Junker_1.prefab"), Vector3.zero, GamePlane.Rotation);
            ship.Initialize(0, 0, Projectiles);
            yield return new WaitForFixedUpdate();

            var dash = ship.Keystone as Dash;
            Assert.That(dash, Is.Not.Null, "Junker_1's live keystone is its Dash");
            Assert.That(ship.BoostAvailable, Is.True);
            var nose = ship.Kinematics.Forward;
            var before = GamePlane.WorldDirToPlane(ship.Rigidbody.linearVelocity);

            ship.Movement.Drive(new PilotCommand { boost = 1f });
            yield return new WaitForFixedUpdate();

            var deltaV = GamePlane.WorldDirToPlane(ship.Rigidbody.linearVelocity) - before;
            var expected = dash.Impulse / ship.Rigidbody.mass;
            Assert.That(deltaV.magnitude, Is.EqualTo(expected).Within(expected * 0.05f), "one Dash adds impulse / mass, less one step of drag");
            Assert.That(Vector2.Dot(deltaV.normalized, nose), Is.GreaterThan(0.99f), "the Dash pushes along the nose");
            Assert.That(ship.Movement.CurrentCommand.boost, Is.EqualTo(1f), "the applied command reports the activation");
            Assert.That(ship.BoostAvailable, Is.False);

            var speedAfterDash = ship.Rigidbody.linearVelocity.magnitude;
            for (var i = 0; i < 25; i++)
            {
                ship.Movement.Drive(new PilotCommand { boost = 1f });
                yield return new WaitForFixedUpdate();
                Assert.That(ship.Movement.CurrentCommand.boost, Is.EqualTo(0f), $"step {i}: held input does not re-fire inside the cooldown");
            }
            Assert.That(ship.Rigidbody.linearVelocity.magnitude, Is.LessThanOrEqualTo(speedAfterDash), "no second impulse; only drag acts");
            Assert.That(ship.BoostCooldownPct, Is.InRange(0f, 1f).And.GreaterThan(0f));

            ship.ResetShip();
            Assert.That(ship.BoostAvailable, Is.True, "a respawn readies the keystone");
        }
    }
}

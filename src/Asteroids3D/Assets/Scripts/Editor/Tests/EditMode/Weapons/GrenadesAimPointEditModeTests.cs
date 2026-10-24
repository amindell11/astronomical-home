using Combat.Projectiles;
using Combat.Weapons.Arsenal;
using Movement;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Weapons
{
    /// <summary>The launcher's AI aim point follows the charge's real flight: a fixed launch speed with no ship velocity, braking evenly to rest, the flight capped at max range.</summary>
    [TestFixture]
    [Category("Weapons")]
    public class GrenadesAimPointEditModeTests
    {
        private const string GrenadesPrefabPath = "Assets/Prefabs/Weapons/Grenades.prefab";
        private const float Dt = 0.02f;

        private static Grenades Launcher()
        {
            var launcher = AssetDatabase.LoadAssetAtPath<Grenades>(GrenadesPrefabPath);
            Assert.IsNotNull(launcher, $"Missing prefab: {GrenadesPrefabPath}");
            return launcher;
        }

        private static Kinematics ShooterAtOrigin(Vector2 vel) => new(Vector2.zero, vel, 0f, 0f, 0f);

        [Test]
        public void AimPoint_IgnoresTheShootersVelocity()
        {
            var launcher = Launcher();
            var targetPos = new Vector2(4f, 15f);
            var targetVel = new Vector2(8f, 0f);

            var still = launcher.AimPoint(ShooterAtOrigin(Vector2.zero), targetPos, targetVel);
            var moving = launcher.AimPoint(ShooterAtOrigin(new Vector2(0f, 15f)), targetPos, targetVel);

            Assert.AreEqual(still, moving, "the charge inherits no ship velocity");
        }

        [Test]
        public void AimPoint_LandsTheBrakedChargeOnACoastingTarget()
        {
            var launcher = Launcher();
            var targetPos = new Vector2(4f, 15f);
            var targetVel = new Vector2(8f, -3f);

            var aim = launcher.AimPoint(ShooterAtOrigin(Vector2.zero), targetPos, targetVel);
            var arrival = BrakedFlightTime(aim.magnitude, launcher.projectilePrefab.LaunchSpeed);

            Assert.Less(aim.magnitude, launcher.Range, "the target is within reach");
            Assert.Less(Vector2.Distance(aim, targetPos + targetVel * arrival), 3f * Dt * targetVel.magnitude,
                "the target reaches the aim point when the charge comes to rest there");
        }

        [Test]
        public void AimPoint_CapsTheFlightAtMaxRange_SoAnUnreachableTargetAimsOutOfRange()
        {
            var launcher = Launcher();
            var cappedFlight = 2f * launcher.Range / launcher.projectilePrefab.LaunchSpeed;
            var shooter = ShooterAtOrigin(Vector2.zero);

            var farPos = new Vector2(0f, launcher.Range + 20f);
            var farVel = new Vector2(5f, 0f);
            var far = launcher.AimPoint(shooter, farPos, farVel);
            Assert.Less(Vector2.Distance(far, farPos + farVel * cappedFlight), 1e-4f, "a target past max range");
            Assert.Greater(far.magnitude, launcher.Range);

            // Receding faster than the charge's mean speed, half its launch speed, it is never caught.
            var fleeingPos = new Vector2(0f, 15f);
            var fleeingVel = new Vector2(0f, 0.5f * launcher.projectilePrefab.LaunchSpeed + 5f);
            var fleeing = launcher.AimPoint(shooter, fleeingPos, fleeingVel);
            Assert.Less(Vector2.Distance(fleeing, fleeingPos + fleeingVel * cappedFlight), 1e-4f, "a target it never catches");
            Assert.Greater(fleeing.magnitude, launcher.Range);
        }

        private static float BrakedFlightTime(float distance, float launchSpeed)
        {
            var travelled = 0f;
            var speed = launchSpeed;
            for (var step = 0; step < 10000; step++)
            {
                var remaining = distance - travelled;
                if (remaining <= 0f || speed <= 0f) return step * Dt;
                speed -= Grenade.BrakingDeceleration(speed, remaining, Dt) * Dt;
                travelled += speed * Dt;
            }

            Assert.Fail("the charge never came to rest");
            return 0f;
        }
    }
}

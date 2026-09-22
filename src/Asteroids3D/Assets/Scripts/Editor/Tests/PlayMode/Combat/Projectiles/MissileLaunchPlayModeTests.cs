using System.Collections;
using Combat;
using Combat.Projectiles;
using NUnit.Framework;
using Substrate;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tests.PlayMode
{
    /// <summary>
    /// Launch velocity of the shipped missile prefab from a moving shooter: the missile inherits only
    /// the shooter's motion along the aim, never a sideways or backward component, and the homing
    /// speed cap still bounds it one physics step later. Arc: #576.
    /// </summary>
    [Category("Weapons")]
    public class MissileLaunchPlayModeTests : PlayModeWorldFixture
    {
        private const string MissilePrefabPath = "Assets/Prefabs/Weapons/MissileProjectile.prefab";

        // Shipped prefab values, restated so the expectations read as numbers.
        private const float InitialSpeed = 7f;
        private const float HomingSpeed = 30f;

        // Fast enough that InitialSpeed + inheritance overshoots the homing cap.
        private const float OverCapShooterSpeed = 34f;

        private static readonly Vector2 Aim = Vector2.up;
        private static readonly Vector2 Across = Vector2.right;

        private Missile missile;
        private StubShooter shooter;

        private sealed class StubShooter : MonoBehaviour, IShooter
        {
            private Rigidbody body;
            public Vector3 Velocity { get; set; }
            public Rigidbody Body => body;
            public Ships.Registry.ShipId Id => Ships.Registry.ShipId.Invalid;
            private void Awake() => body = GetComponent<Rigidbody>();
        }

        public override void TearDown()
        {
            DestroyTestObject(missile);
            DestroyTestObject(shooter);
            base.TearDown();
        }

        [TestCase(25f, 0f, InitialSpeed, 0f, TestName = "Strafing_LaunchesAlongTheAim")]
        [TestCase(0f, -25f, InitialSpeed, 0f, TestName = "DriftingBackward_LaunchesForwardAtInitialSpeed")]
        [TestCase(0f, OverCapShooterSpeed, InitialSpeed + OverCapShooterSpeed, 0f, TestName = "FlyingForward_AddsTheShooterSpeed")]
        public void Launch_InheritsOnlyTheShooterMotionAlongTheAim(
            float shooterAcross, float shooterAlong, float expectedAlong, float expectedAcross)
        {
            Fire(shooterAcross, shooterAlong);

            var (along, across) = LaunchComponents();
            TestContext.WriteLine($"shooter=({shooterAcross},{shooterAlong}) launch: along={along:F2} across={across:F2}");
            Assert.That(along, Is.EqualTo(expectedAlong).Within(0.05f), "along-aim launch speed");
            Assert.That(across, Is.EqualTo(expectedAcross).Within(0.05f), "across-aim launch speed");
        }

        [UnityTest]
        public IEnumerator FlyingForwardFasterThanHoming_IsCappedAtHomingSpeedNextStep()
        {
            Fire(0f, OverCapShooterSpeed);
            yield return new WaitForFixedUpdate();

            var speed = missile.GetComponent<Rigidbody>().linearVelocity.magnitude;
            TestContext.WriteLine($"speed after one physics step={speed:F2}");
            Assert.That(speed, Is.EqualTo(HomingSpeed).Within(0.05f));
        }

        private void Fire(float shooterAcross, float shooterAlong)
        {
            shooter = new GameObject("Shooter").AddComponent<StubShooter>();
            shooter.Velocity = GamePlane.PlaneDirToWorld(Across * shooterAcross + Aim * shooterAlong);

#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<Missile>(MissilePrefabPath);
            Assert.IsNotNull(prefab, $"Failed to load {MissilePrefabPath}");
            var worldAim = GamePlane.PlaneDirToWorld(Aim);
            missile = Object.Instantiate(prefab, Vector3.zero, Quaternion.LookRotation(GamePlane.Normal, worldAim));
            missile.Initialize(shooter);
            missile.Launch(worldAim);
#else
            Assert.Ignore("Requires Unity Editor assets.");
#endif
        }

        private (float along, float across) LaunchComponents()
        {
            var v = GamePlane.WorldDirToPlane(missile.GetComponent<Rigidbody>().linearVelocity);
            return (Vector2.Dot(v, Aim), Vector2.Dot(v, Across));
        }
    }
}

using System.Collections;
using Combat;
using Combat.Projectile;
using Game;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    [Category("Weapons")]
    [Category("Slow")]
    public class MissileGuidancePlayModeTests : PlayModeWorldFixture
    {
        private Missile missile;
        private GameObject targetGo;

        private class StubShooter : MonoBehaviour, IShooter
        {
            public Vector3 Velocity { get; set; }
        }

        public override void TearDown()
        {
            DestroyTestObject(missile);
            DestroyTestObject(targetGo);
            base.TearDown();
        }

        private Missile CreateTestMissile(Vector3 worldPos)
        {
            var go = new GameObject("TestMissile");
            go.transform.position = worldPos;

            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;

            var m = go.AddComponent<Missile>();
            m.Configure(maxDistance: 200f, maxLifetime: 10f);

            return m;
        }

        private GameObject CreateTarget(Vector2 planePos)
        {
            var go = new GameObject("Target");
            go.transform.position = GamePlane.PlanePointToWorld(planePos);
            return go;
        }

        private void LaunchAt(Missile m, Vector2 planeDir, IShooter shooter)
        {
            // Align transform.up to the launch direction on the game plane,
            // matching how WeaponBase.Fire() sets firePoint.rotation before Launch().
            // KinematicsPoller reads transform.up to compute yaw/Forward, so without
            // this the guidance system sees a wrong heading and cannot converge.
            var worldDir = GamePlane.PlaneDirToWorld(planeDir.normalized);
            m.transform.rotation = Quaternion.LookRotation(GamePlane.Normal, worldDir);

            m.Initialize(shooter);
            m.Launch(worldDir);
        }

        private float DistanceToTarget()
        {
            return Vector3.Distance(missile.transform.position, targetGo.transform.position);
        }

        [UnityTest]
        public IEnumerator StationaryFire_DistantTarget_Converges()
        {
            var origin = GamePlane.PlanePointToWorld(Vector2.zero);
            missile = CreateTestMissile(origin);
            targetGo = CreateTarget(new Vector2(0, 20));

            var shooter = new GameObject("Shooter").AddComponent<StubShooter>();
            missile.SetTarget(targetGo.transform);
            LaunchAt(missile, Vector2.up, shooter);

            yield return AsyncAssert.WaitUntil(
                () => DistanceToTarget() < 2f,
                5f,
                $"Missile did not converge on distant target (dist={DistanceToTarget():F2})",
                useFixedUpdate: true);

            DestroyTestObject(shooter);
        }

        [UnityTest]
        public IEnumerator StationaryFire_CloseTarget_NoOrbit()
        {
            var origin = GamePlane.PlanePointToWorld(Vector2.zero);
            missile = CreateTestMissile(origin);
            targetGo = CreateTarget(new Vector2(0, 3));

            var shooter = new GameObject("Shooter").AddComponent<StubShooter>();
            missile.SetTarget(targetGo.transform);
            LaunchAt(missile, Vector2.up, shooter);

            var startDist = DistanceToTarget();
            var peakDist = startDist;
            var settled = false;
            var settleTime = 0.3f;
            var elapsed = 0f;
            var converged = false;

            while (elapsed < 2f)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;

                var dist = DistanceToTarget();
                if (elapsed > settleTime)
                {
                    if (dist > peakDist) peakDist = dist;
                    settled = true;
                }

                if (dist < 1.5f)
                {
                    converged = true;
                    break;
                }
            }

            Assert.IsTrue(converged,
                $"Missile did not reach close target within 2s (dist={DistanceToTarget():F2})");

            if (settled)
            {
                Assert.LessOrEqual(peakDist, startDist + 1f,
                    $"Missile overshot close target — peak distance {peakDist:F2} exceeded start {startDist:F2} + 1");
            }

            DestroyTestObject(shooter);
        }

        [UnityTest]
        public IEnumerator MovingShooter_StationaryTarget_Converges()
        {
            var origin = GamePlane.PlanePointToWorld(Vector2.zero);
            missile = CreateTestMissile(origin);
            targetGo = CreateTarget(new Vector2(0, 15));

            var shooterGo = new GameObject("Shooter");
            var shooter = shooterGo.AddComponent<StubShooter>();
            shooter.Velocity = GamePlane.PlaneDirToWorld(new Vector2(10, 0));

            missile.SetTarget(targetGo.transform);
            LaunchAt(missile, Vector2.up, shooter);

            yield return AsyncAssert.WaitUntil(
                () => DistanceToTarget() < 2f,
                5f,
                $"Missile with moving shooter did not converge (dist={DistanceToTarget():F2})",
                useFixedUpdate: true);

            DestroyTestObject(shooterGo);
        }

        [UnityTest]
        public IEnumerator MovingShooter_MovingTarget_Tracks()
        {
            var origin = GamePlane.PlanePointToWorld(Vector2.zero);
            missile = CreateTestMissile(origin);
            targetGo = CreateTarget(new Vector2(0, 10));

            var targetRb = targetGo.AddComponent<Rigidbody>();
            targetRb.useGravity = false;
            targetRb.linearVelocity = GamePlane.PlaneDirToWorld(new Vector2(5, 0));

            var shooterGo = new GameObject("Shooter");
            var shooter = shooterGo.AddComponent<StubShooter>();

            missile.SetTarget(targetGo.transform);
            LaunchAt(missile, Vector2.up, shooter);

            yield return AsyncAssert.WaitUntil(
                () => DistanceToTarget() < 3f,
                5f,
                $"Missile did not track moving target (dist={DistanceToTarget():F2})",
                useFixedUpdate: true);

            DestroyTestObject(shooterGo);
        }

        [UnityTest]
        public IEnumerator TargetBehind_EventuallyConverges()
        {
            var origin = GamePlane.PlanePointToWorld(Vector2.zero);
            missile = CreateTestMissile(origin);
            targetGo = CreateTarget(new Vector2(0, -15));

            var shooter = new GameObject("Shooter").AddComponent<StubShooter>();
            missile.SetTarget(targetGo.transform);
            LaunchAt(missile, Vector2.up, shooter);

            yield return AsyncAssert.WaitUntil(
                () => DistanceToTarget() < 3f,
                8f,
                $"Missile did not converge on target behind (dist={DistanceToTarget():F2})",
                useFixedUpdate: true);

            DestroyTestObject(shooter);
        }

        [UnityTest]
        [Ignore("Known guidance limitation: pure pursuit cannot converge on 90° offset. Remove when guidance is improved.")]
        public IEnumerator Target90Degrees_Converges()
        {
            var origin = GamePlane.PlanePointToWorld(Vector2.zero);
            missile = CreateTestMissile(origin);
            targetGo = CreateTarget(new Vector2(15, 0));

            var shooter = new GameObject("Shooter").AddComponent<StubShooter>();
            missile.SetTarget(targetGo.transform);
            LaunchAt(missile, Vector2.up, shooter);

            yield return AsyncAssert.WaitUntil(
                () => DistanceToTarget() < 3f,
                8f,
                $"Missile did not converge on 90-degree target (dist={DistanceToTarget():F2})",
                useFixedUpdate: true);

            DestroyTestObject(shooter);
        }

        [UnityTest]
        public IEnumerator CloseRangeLock_NoOvershootOrbit()
        {
            var origin = GamePlane.PlanePointToWorld(Vector2.zero);
            missile = CreateTestMissile(origin);
            targetGo = CreateTarget(new Vector2(0, 2.5f));

            var shooter = new GameObject("Shooter").AddComponent<StubShooter>();
            missile.SetTarget(targetGo.transform);
            LaunchAt(missile, Vector2.up, shooter);

            var startDist = DistanceToTarget();
            var peakDist = startDist;
            var elapsed = 0f;
            var converged = false;

            while (elapsed < 2f)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;

                var dist = DistanceToTarget();
                if (dist > peakDist) peakDist = dist;

                if (dist < 1f)
                {
                    converged = true;
                    break;
                }
            }

            Assert.IsTrue(converged,
                $"Missile did not reach close-range target within 2s (dist={DistanceToTarget():F2})");
            Assert.LessOrEqual(peakDist, startDist + 1f,
                $"Missile overshot/orbited close target — peak {peakDist:F2} > start {startDist:F2} + 1");

            DestroyTestObject(shooter);
        }
    }
}

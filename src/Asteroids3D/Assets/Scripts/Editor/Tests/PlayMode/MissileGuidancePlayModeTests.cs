using System.Collections;
using Combat;
using Combat.Projectiles;
using Combat.Projectiles.Visual;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Substrate;

namespace Tests.PlayMode
{
    [Category("Weapons")]
    public class MissileGuidancePlayModeTests : PlayModeWorldFixture
    {
        private Missile missile;
        private GameObject targetGo;
        private GameObject rock;
        private StubShooter shooter;

        protected override bool AccelerateTime => true;

        private class StubShooter : MonoBehaviour, IShooter
        {
            public Vector3 Velocity => Vector3.zero;
            public Rigidbody Body => GetComponent<Rigidbody>();
            public Ships.Registry.ShipId Id => Ships.Registry.ShipId.Invalid;
        }

        public override void TearDown()
        {
            DestroyTestObject(missile);
            DestroyTestObject(targetGo);
            DestroyTestObject(rock);
            DestroyTestObject(shooter);
            base.TearDown();
        }

        private Missile CreateTestMissile(Vector3 worldPos)
        {
            var go = new GameObject("TestMissile");
            go.transform.position = worldPos;

            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            go.AddComponent<CapsuleCollider>().isTrigger = true;

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
            // KinematicsPoller derives heading from transform.up, so align it to the launch direction (as WeaponBase.Fire does) or guidance can't converge.
            var worldDir = GamePlane.PlaneDirToWorld(planeDir.normalized);
            m.transform.rotation = Quaternion.LookRotation(GamePlane.Normal, worldDir);

            m.Initialize(shooter);
            m.Launch(worldDir, m.transform.position + worldDir);
        }

        private GameObject CreateRock(Vector2 planePos)
        {
            var go = new GameObject("Rock") { layer = LayerIds.Asteroid };
            go.transform.position = GamePlane.PlanePointToWorld(planePos);
            go.AddComponent<SphereCollider>();
            Physics.SyncTransforms();
            return go;
        }

        private float DistanceToTarget()
        {
            return Vector3.Distance(missile.transform.position, targetGo.transform.position);
        }

        private float OffNoseAngle()
        {
            var nose = GamePlane.WorldDirToPlane(missile.transform.up);
            var toTarget = GamePlane.WorldDirToPlane(targetGo.transform.position - missile.transform.position);
            return Vector2.Angle(nose, toTarget);
        }

        private static IEnumerator FlyFor(float seconds)
        {
            for (var t = 0f; t < seconds; t += Time.fixedDeltaTime)
                yield return new WaitForFixedUpdate();
        }

        private bool BodyTintedRed()
        {
            var body = missile.GetComponent<MeshRenderer>();
            if (!body.HasPropertyBlock()) return false;
            var block = new MaterialPropertyBlock();
            body.GetPropertyBlock(block);
            return block.GetColor("_BaseColor") == Color.red;
        }

        private void AssertFlewStraightUp()
        {
            var nose = GamePlane.WorldDirToPlane(missile.transform.up);
            var pos = GamePlane.WorldPointToPlane(missile.transform.position);
            Assert.That(Vector2.Angle(nose, Vector2.up), Is.LessThan(0.5f), "Missile turned after losing track");
            Assert.That(Mathf.Abs(pos.x), Is.LessThan(0.05f), "Missile drifted off its launch line after losing track");
        }

        [UnityTest]
        public IEnumerator StationaryFire_DistantTarget_Converges()
        {
            var origin = GamePlane.PlanePointToWorld(Vector2.zero);
            missile = CreateTestMissile(origin);
            targetGo = CreateTarget(new Vector2(0, 20));

            shooter = new GameObject("Shooter").AddComponent<StubShooter>();
            missile.SetTarget(targetGo.transform);
            LaunchAt(missile, Vector2.up, shooter);

            yield return AsyncAssert.WaitUntil(
                () => DistanceToTarget() < 2f,
                5f,
                $"Missile did not converge on distant target (dist={DistanceToTarget():F2})",
                useFixedUpdate: true);
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

            shooter = new GameObject("Shooter").AddComponent<StubShooter>();

            missile.SetTarget(targetGo.transform);
            LaunchAt(missile, Vector2.up, shooter);

            yield return AsyncAssert.WaitUntil(
                () => DistanceToTarget() < 3f,
                5f,
                $"Missile did not track moving target (dist={DistanceToTarget():F2})",
                useFixedUpdate: true);
        }

        [UnityTest]
        public IEnumerator TargetBehind_LosesTrackAndFliesStraight()
        {
            var origin = GamePlane.PlanePointToWorld(Vector2.zero);
            missile = CreateTestMissile(origin);
            targetGo = CreateTarget(new Vector2(0, -15));

            shooter = new GameObject("Shooter").AddComponent<StubShooter>();
            missile.SetTarget(targetGo.transform);
            LaunchAt(missile, Vector2.up, shooter);

            yield return new WaitForFixedUpdate();
            Assert.That(missile.target, Is.Null, "Missile kept tracking a target behind its seeker cone");

            yield return FlyFor(1f);
            AssertFlewStraightUp();
        }

        [UnityTest]
        public IEnumerator CrossingTarget_LeavesSeekerCone_LosesTrack()
        {
            var origin = GamePlane.PlanePointToWorld(Vector2.zero);
            missile = CreateTestMissile(origin);
            targetGo = CreateTarget(new Vector2(-1, 3));

            // Crosses the nose faster than the missile can turn after it.
            var targetRb = targetGo.AddComponent<Rigidbody>();
            targetRb.useGravity = false;
            targetRb.linearVelocity = GamePlane.PlaneDirToWorld(new Vector2(40, 0));

            shooter = new GameObject("Shooter").AddComponent<StubShooter>();
            missile.SetTarget(targetGo.transform);
            LaunchAt(missile, Vector2.up, shooter);

            var halfCone = missile.seekerConeAngle * 0.5f;
            Assert.That(OffNoseAngle(), Is.LessThan(halfCone), "Precondition: the target starts inside the seeker cone");

            // The pose the test sees before a step is the pose the missile checks during it.
            var angleOnLastTrackedStep = OffNoseAngle();
            for (var elapsed = 0f; missile.target && elapsed < 1f; elapsed += Time.fixedDeltaTime)
            {
                angleOnLastTrackedStep = OffNoseAngle();
                yield return new WaitForFixedUpdate();
            }

            Assert.That(missile.target, Is.Null, "Missile kept tracking a target that crossed out of its seeker cone");
            Assert.That(angleOnLastTrackedStep, Is.GreaterThan(halfCone),
                "Missile lost track while the target was still inside its seeker cone");
        }

        [UnityTest]
        public IEnumerator RockOnLineOfSight_LosesTrack()
        {
            var origin = GamePlane.PlanePointToWorld(Vector2.zero);
            missile = CreateTestMissile(origin);
            targetGo = CreateTarget(new Vector2(6, 6));
            // Blocks the sightline but sits 3 u off the flight path: never hit.
            rock = CreateRock(new Vector2(3, 3));

            shooter = new GameObject("Shooter").AddComponent<StubShooter>();
            missile.SetTarget(targetGo.transform);
            LaunchAt(missile, Vector2.up, shooter);

            Assert.That(OffNoseAngle(), Is.LessThan(missile.seekerConeAngle * 0.5f),
                "Precondition: the target starts inside the seeker cone");

            yield return new WaitForFixedUpdate();
            Assert.That(missile.target, Is.Null, "Missile kept tracking a target hidden behind a rock");

            yield return FlyFor(0.5f);
            AssertFlewStraightUp();
        }

        [Test]
        public void Dumbfire_TintsBodyRed()
        {
            missile = CreateTestMissile(GamePlane.PlanePointToWorld(Vector2.zero));
            missile.gameObject.AddComponent<MissileTrackingTint>();

            shooter = new GameObject("Shooter").AddComponent<StubShooter>();
            LaunchAt(missile, Vector2.up, shooter);

            Assert.That(BodyTintedRed(), Is.True, "Dumbfired missile body is not tinted");
        }

        [UnityTest]
        public IEnumerator LockedLaunch_TintsBodyRedOnlyAfterLosingTrack()
        {
            missile = CreateTestMissile(GamePlane.PlanePointToWorld(Vector2.zero));
            missile.gameObject.AddComponent<MissileTrackingTint>();
            targetGo = CreateTarget(new Vector2(0, -15));

            shooter = new GameObject("Shooter").AddComponent<StubShooter>();
            // Missiles.Fire launches first, then hands over the locked target.
            LaunchAt(missile, Vector2.up, shooter);
            missile.SetTarget(targetGo.transform);
            Assert.That(BodyTintedRed(), Is.False, "Missile body tinted while it was tracking");

            yield return new WaitForFixedUpdate();
            Assert.That(missile.IsTracking, Is.False, "Precondition: the missile lost track of a target behind it");
            Assert.That(BodyTintedRed(), Is.True, "Missile body not tinted after it lost track");
        }

        [UnityTest]
        public IEnumerator CloseRangeLock_NoOvershootOrbit()
        {
            var origin = GamePlane.PlanePointToWorld(Vector2.zero);
            missile = CreateTestMissile(origin);
            targetGo = CreateTarget(new Vector2(0, 2.5f));

            shooter = new GameObject("Shooter").AddComponent<StubShooter>();
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
        }
    }
}

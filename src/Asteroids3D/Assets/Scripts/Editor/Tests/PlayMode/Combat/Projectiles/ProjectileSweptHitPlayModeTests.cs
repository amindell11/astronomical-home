using System.Collections;
using System.Collections.Generic;
using Combat;
using Combat.Projectiles;
using Damage;
using NUnit.Framework;
using Substrate;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tests.PlayMode
{
    [Category("Weapons")]
    public class ProjectileSweptHitPlayModeTests : PlayModeWorldFixture
    {
        private const string LaserPrefabPath = "Assets/Prefabs/Weapons/Projectiles/Laser.prefab";
        private const string MissilePrefabPath = "Assets/Prefabs/Weapons/Projectiles/MissileProjectile.prefab";

        // Laser.prefab's capsule in world units: radius 0.1 × 0.35, height 0.4 × 0.714.
        private const float LaserRadius = 0.035f;
        private const float LaserHalfLength = 0.1428f;

        private const float TargetRadius = 0.1f;
        private const float Margin = 0.02f;
        private const int StepsToPass = 5;

        // Inside the missile quad's half-length (0.452) along the axis.
        private const float TipProbe = 0.45f;

        private static readonly Vector3 WorldAim = GamePlane.PlaneDirToWorld(Vector2.up);
        private static readonly Vector3 WorldAcross = GamePlane.PlaneDirToWorld(Vector2.right);

        private readonly List<GameObject> spawned = new();

        private sealed class StubShooter : MonoBehaviour, IShooter
        {
            public Vector3 Velocity => Vector3.zero;
            public Rigidbody Body => null;
            public Ships.Registry.ShipId Id => Ships.Registry.ShipId.Invalid;
        }

        private sealed class DamageRecorder : MonoBehaviour, IDamageable
        {
            public int Hits { get; private set; }
            public void TakeDamage(in DamageInfo hit) => Hits++;
        }

        public override void TearDown()
        {
            // Hit bursts (untracked PooledVFX) outlive the test and trip the grenade test fixture's zero-VFX assertion.
            foreach (var vfx in Object.FindObjectsByType<PooledVFX>(FindObjectsSortMode.None))
                Object.DestroyImmediate(vfx.gameObject);

            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator LaserStepStraddlingAThinTarget_HitsItAtTheContactPose()
        {
            var target = CreateTarget(LayerIds.Ship);
            var laser = FireLaserPastTarget(0f);
            Vector3? hitAt = null;
            laser.Hit += (point, _) => hitAt = point;

            yield return new WaitForFixedUpdate();

            Assert.That(target.Hits, Is.EqualTo(1), "the straddled target took the hit");
            Assert.That(hitAt.HasValue, "the laser raised Hit");
            var contactAlong = Vector3.Dot(hitAt.Value, WorldAim);
            TestContext.WriteLine($"hit at along={contactAlong:F3}");
            Assert.That(contactAlong, Is.EqualTo(-(TargetRadius + LaserHalfLength)).Within(0.02f),
                "the shot moved to where its nose met the target");
        }

        [UnityTest]
        public IEnumerator LaserPathJustOutsideTheCombinedRadius_Misses()
        {
            var target = CreateTarget(LayerIds.Ship);
            var laser = FireLaserPastTarget(TargetRadius + LaserRadius + Margin);

            yield return StepPastTarget(laser);

            Assert.That(target.Hits, Is.Zero);
        }

        [UnityTest]
        public IEnumerator TargetOnALayerTheShotIgnores_IsNeverHit()
        {
            var shipBody = LayerMask.NameToLayer("ShipBody");
            Assume.That(Physics.GetIgnoreLayerCollision(LayerIds.Projectile, shipBody), "Projectile ignores ShipBody");
            var target = CreateTarget(shipBody);
            var laser = FireLaserPastTarget(0f);

            yield return StepPastTarget(laser);

            Assert.That(target.Hits, Is.Zero);
        }

        [TestCase(-Margin, true, TestName = "MissileHitbox_ProbeJustInsideTheRadius_Overlaps")]
        [TestCase(Margin, false, TestName = "MissileHitbox_ProbeJustOutsideTheRadius_DoesNot")]
        public void MissileHitbox_AcrossTheAxis(float beyondRadius, bool overlaps)
        {
            var missile = SpawnMissile(out var capsule);

            Assert.That(Contains(capsule, WorldAcross * (missile.hitboxRadius + beyondRadius)), Is.EqualTo(overlaps));
        }

        [Test]
        public void MissileHitbox_ProbeNearTheTip_Overlaps()
        {
            SpawnMissile(out var capsule);

            Assert.That(Contains(capsule, WorldAim * TipProbe), Is.True);
        }

        private DamageRecorder CreateTarget(int layer)
        {
            var go = new GameObject("ThinTarget") { layer = layer };
            spawned.Add(go);
            var sphere = go.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = TargetRadius;
            return go.AddComponent<DamageRecorder>();
        }

        // Starts half a step short of the target so one step straddles it.
        private Laser FireLaserPastTarget(float acrossOffset)
        {
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<Laser>(LaserPrefabPath);
            Assert.IsNotNull(prefab, $"Failed to load {LaserPrefabPath}");
            var halfStep = prefab.LaserSpeed * Time.fixedDeltaTime * 0.5f;
            Assume.That(halfStep, Is.GreaterThan(TargetRadius + LaserHalfLength), "neither end of the step overlaps the target");

            var shooter = new GameObject("Shooter").AddComponent<StubShooter>();
            spawned.Add(shooter.gameObject);
            shooter.transform.position = WorldAcross * 10f;

            var start = WorldAcross * acrossOffset - WorldAim * halfStep;
            var laser = Object.Instantiate(prefab, start, GamePlane.PlanePose(GamePlane.Normal, WorldAim));
            spawned.Add(laser.gameObject);
            laser.Initialize(shooter);
            laser.Launch(WorldAim, start + WorldAim);
            return laser;
#else
            Assert.Ignore("Requires Unity Editor assets.");
            return null;
#endif
        }

        private static IEnumerator StepPastTarget(Laser laser)
        {
            for (var i = 0; i < StepsToPass; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Dot(laser.transform.position, WorldAim), Is.GreaterThan(LaserHalfLength + TargetRadius),
                "the laser flew past the target");
        }

        private Missile SpawnMissile(out CapsuleCollider capsule)
        {
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<Missile>(MissilePrefabPath);
            Assert.IsNotNull(prefab, $"Failed to load {MissilePrefabPath}");
            var missile = Object.Instantiate(prefab, Vector3.zero, GamePlane.PlanePose(GamePlane.Normal, WorldAim));
            spawned.Add(missile.gameObject);
            capsule = missile.GetComponent<CapsuleCollider>();
            Physics.SyncTransforms();
            return missile;
#else
            Assert.Ignore("Requires Unity Editor assets.");
            capsule = null;
            return null;
#endif
        }

        private static bool Contains(Collider collider, Vector3 point) =>
            (collider.ClosestPoint(point) - point).sqrMagnitude < 1e-8f;
    }
}

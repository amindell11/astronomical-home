#if UNITY_EDITOR && ASTRO_FMOD
using System.Collections;
using System.Collections.Generic;
using Combat;
using Combat.Projectiles;
using Damage;
using NUnit.Framework;
using Ships.Registry;
using Substrate;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Audio
{
    [Category("Audio")]
    public class LaserImpactAudioPlayModeTests : FmodEvidenceFixture
    {
        private const string ProjectileFolder = "Assets/Prefabs/Weapons/Projectiles/";
        private const float TargetDistance = 3f;
        private const int MaxSteps = 50;

        private static readonly Vector3 WorldAim = GamePlane.PlaneDirToWorld(Vector2.up);

        private sealed class StubShooter : MonoBehaviour, IShooter
        {
            public Vector3 Velocity => Vector3.zero;
            public Rigidbody Body => null;
            public ShipId Id => ShipId.Invalid;
        }

        private sealed class InertTarget : MonoBehaviour, IDamageable
        {
            public void TakeDamage(in DamageInfo hit) { }
        }

        private readonly List<GameObject> spawned = new();

        [TearDown]
        public override void TearDown()
        {
            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator LaserHit_PlaysOneLaserImpact() =>
            OneImpactPerHit("Laser", "event:/Weapons/Laser/Impact");

        [UnityTest]
        public IEnumerator TurboLaserHit_PlaysOneTurboLaserImpact() =>
            OneImpactPerHit("TurboLaser", "event:/Weapons/TurboLaser/Impact");

        [UnityTest]
        public IEnumerator ChargeBoltHit_PlaysOneChargeLaserImpact() =>
            OneImpactPerHit("ChargeBolt", "event:/Weapons/ChargeLaser/Impact");

        [UnityTest]
        public IEnumerator RipperSlugHit_PlaysOneRipperImpact() =>
            OneImpactPerHit("RipperSlug", "event:/Weapons/Ripper/Impact");

        private IEnumerator OneImpactPerHit(string prefabName, string impactEvent)
        {
            var impact = Fresh(impactEvent);
            Target(WorldAim * TargetDistance);
            var projectile = Fire(prefabName);
            Vector3? hitAt = null;
            projectile.Hit += (point, _) => hitAt = point;

            for (var i = 0; i < MaxSteps && !hitAt.HasValue; i++)
                yield return new WaitForFixedUpdate();
            Assert.IsTrue(hitAt.HasValue, $"the {prefabName} reaches the target");
            yield return Flush();

            var instances = Instances(impact);
            Debug.Log($"[FMOD] {impactEvent} instances={instances.Length} after one {prefabName} hit at {hitAt}");
            Assert.AreEqual(1, instances.Length, "one impact per hit");
            Assert.Less(Vector3.Distance(Position(instances[0]), hitAt.Value), 0.01f, "played at the hit point");
            AssertPlaying(instances[0], "the impact still sounds a frame later");
        }

        private void Target(Vector3 position)
        {
            var go = new GameObject("Target") { layer = LayerIds.Ship };
            spawned.Add(go);
            go.transform.position = position;
            var sphere = go.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 0.5f;
            go.AddComponent<InertTarget>();
        }

        private Laser Fire(string prefabName)
        {
            var path = ProjectileFolder + prefabName + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<Laser>(path);
            Assert.IsNotNull(prefab, $"Failed to load {path}");

            var shooter = new GameObject("Shooter").AddComponent<StubShooter>();
            spawned.Add(shooter.gameObject);
            var projectile = Object.Instantiate(prefab, Vector3.zero, GamePlane.PlanePose(GamePlane.Normal, WorldAim));
            spawned.Add(projectile.gameObject);
            projectile.Initialize(shooter);
            projectile.Launch(WorldAim, WorldAim * TargetDistance);
            return projectile;
        }
    }
}
#endif

using System.Collections;
using System.Collections.Generic;
using Combat;
using Combat.Projectiles;
using Combat.Projectiles.Visual;
using Damage;
using NUnit.Framework;
using Ships.Registry;
using Substrate;
using Substrate.Services.Projectiles;
using Tests.Common;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Tests.PlayMode.Projectiles
{
    [Category("Presentation")]
    public class LaserHitVfxPlayModeTests : PlayModeWorldFixture
    {
        private sealed class Shooter : MonoBehaviour, IShooter
        {
            public Vector3 Velocity => Vector3.zero;
            public Rigidbody Body => null;
            public ShipId Id => ShipId.Invalid;
        }

        private sealed class Target : MonoBehaviour, IDamageable
        {
            public float TakeDamage(in DamageInfo hit) => 0f;

            private void OnDestroy() => DamageableColliders.Unregister(this);
        }

        private readonly List<GameObject> spawned = new();
        private Vector3? hitAt;

        public override void TearDown()
        {
            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();
            foreach (var vfx in Object.FindObjectsByType<PooledVFX>(FindObjectsSortMode.None))
                Object.DestroyImmediate(vfx.gameObject);
            SimplePool<PooledVFX>.Clear();
            SimplePool<Laser>.Clear();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator LaserHit_UsesContactAndOrientation_ExpiresAndReplaysFromPool()
        {
            yield return HitCycle("Laser", "LaserImpact");
        }

        [UnityTest]
        public IEnumerator TurboLaserHit_UsesItsOwnImpact_ExpiresAndReplaysFromPool()
        {
            yield return HitCycle("TurboLaser", "TurboLaserImpact");
        }

        [UnityTest]
        public IEnumerator RipperHit_UsesItsOwnImpact_ExpiresAndReplaysFromPool()
        {
            yield return HitCycle("RipperSlug", "RipperImpact");
        }

        private IEnumerator HitCycle(string projectileName, string effectName)
        {
            Fire(projectileName, Vector3.up, Projectiles);
            yield return WaitForHit();
            var first = OnlyImpact();
            var material = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Visuals/Vfx/{effectName}/Materials/{effectName}.mat");
            Assert.IsTrue(material);
            Assert.AreSame(material, first.GetComponent<ParticleSystemRenderer>().sharedMaterial);
            Assert.Less(Vector3.Distance(first.transform.position, hitAt.Value), 0.001f);
            Assert.Less(Vector3.Angle(first.transform.up, Vector3.up), 0.01f);
            Assert.Greater(first.GetComponent<ParticleSystem>().particleCount, 0);

            yield return new WaitForSeconds(0.4f);
            Assert.IsFalse(first.gameObject.activeSelf, "the short impact returns to its pool");

            Fire(projectileName, Vector3.right, Projectiles);
            yield return WaitForHit();
            var second = OnlyImpact();
            Assert.AreSame(first, second, "the next hit reuses the expired impact");
            Assert.Less(Vector3.Distance(second.transform.position, hitAt.Value), 0.001f);
            Assert.Less(Vector3.Angle(second.transform.up, Vector3.right), 0.01f);
            Assert.Greater(second.GetComponent<ParticleSystem>().particleCount, 0);
        }

        [UnityTest]
        public IEnumerator HeadlessProjectileHits_SpawnNoImpact()
        {
            var host = Track(new GameObject("Headless projectiles"));
            var headless = new ProjectileService(host.transform, presentationEnabled: false);
            foreach (var name in new[] { "Laser", "TurboLaser", "RipperSlug" })
            {
                var projectile = Fire(name, Vector3.up, headless);
                foreach (var renderer in projectile.GetComponentsInChildren<Renderer>())
                    Assert.IsFalse(renderer.enabled, $"{name}'s renderer must be dark headless");
                yield return WaitForHit();
                Assert.IsEmpty(Object.FindObjectsByType<PooledVFX>(FindObjectsSortMode.None));
            }
        }

        [UnityTest]
        public IEnumerator RipperVisual_FadesDuringFlight_AndRestoresOnReuse()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Laser>("Assets/Prefabs/Weapons/Projectiles/RipperSlug.prefab");
            var shooter = Track(new GameObject("Shooter")).AddComponent<Shooter>();
            var projectile = SimplePool<Laser>.Get(prefab, Vector3.zero, Quaternion.identity);
            Projectiles.Register(projectile, projectile.ReturnToPoolImmediate);
            projectile.Initialize(shooter);
            projectile.Launch(Vector3.up, Vector3.up * 50f);
            var renderer = projectile.transform.Find("Slug").GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            var baseColor = Shader.PropertyToID("_BaseColor");
            while (projectile.gameObject.activeSelf && projectile.DistanceTraveled < projectile.MaxDistance * .85f)
                yield return new WaitForFixedUpdate();
            Assert.IsTrue(projectile.gameObject.activeSelf, "sample the moving slug before range expiry");
            yield return null;
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetColor(baseColor).a, Is.InRange(0.01f, .9f));
            projectile.ReturnToPoolImmediate();
            var reused = SimplePool<Laser>.Get(prefab, Vector3.zero, Quaternion.identity);
            Assert.AreSame(projectile, reused);
            Projectiles.Register(reused, reused.ReturnToPoolImmediate);
            reused.Initialize(shooter);
            reused.Launch(Vector3.up, Vector3.up * 50f);
            yield return null;
            renderer.GetPropertyBlock(block);
            Assert.Greater(block.GetColor(baseColor).a, .95f, "the reused child must regain full opacity");
            reused.ReturnToPoolImmediate();
        }

        [UnityTest]
        public IEnumerator Bolts_StayVisible_UntilTheyDespawnAtRangeEnd()
        {
            var block = new MaterialPropertyBlock();
            var baseColor = Shader.PropertyToID("_BaseColor");
            foreach (var name in new[] { "Laser", "TurboLaser", "RipperSlug" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<Laser>($"Assets/Prefabs/Weapons/Projectiles/{name}.prefab");
                var shooter = Track(new GameObject("Shooter")).AddComponent<Shooter>();
                var projectile = SimplePool<Laser>.Get(prefab, Vector3.zero, Quaternion.identity);
                Projectiles.Register(projectile, projectile.ReturnToPoolImmediate);
                projectile.Initialize(shooter);
                projectile.Launch(Vector3.up, Vector3.up * 50f);

                var renderers = projectile.GetComponentsInChildren<Renderer>();
                var minAlpha = 1f;
                var lastNormalized = 0f;
                var deadline = Time.time + 5f;
                while (projectile.gameObject.activeSelf && Time.time < deadline)
                {
                    yield return null;
                    if (!projectile.gameObject.activeSelf)
                        break;
                    lastNormalized = projectile.DistanceTraveled / projectile.MaxDistance;
                    foreach (var renderer in renderers)
                    {
                        renderer.GetPropertyBlock(block);
                        minAlpha = Mathf.Min(minAlpha, block.GetColor(baseColor).a);
                    }
                }

                Assert.IsFalse(projectile.gameObject.activeSelf, $"{name} did not despawn");
                Assert.That(lastNormalized, Is.GreaterThanOrEqualTo(0.9f), $"{name} samples must reach the end of range");
                Assert.That(minAlpha, Is.GreaterThanOrEqualTo(LaserVisual.AlphaAtRangeEnd - 0.001f),
                    $"{name} must read until it despawns");
            }
        }

        private Laser Fire(string prefabName, Vector3 direction, ProjectileService projectiles)
        {
            hitAt = null;
            var target = Track(new GameObject("Target") { layer = LayerIds.Ship });
            target.transform.position = direction * 3f;
            var collider = target.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.5f;
            DamageableColliderRegistration.Register(target.AddComponent<Target>());
            var shooter = Track(new GameObject("Shooter")).AddComponent<Shooter>();
            var prefab = AssetDatabase.LoadAssetAtPath<Laser>($"Assets/Prefabs/Weapons/Projectiles/{prefabName}.prefab");
            var projectile = SimplePool<Laser>.Get(prefab, Vector3.zero, GamePlane.PlanePose(GamePlane.Normal, direction));
            projectiles.Register(projectile, projectile.ReturnToPoolImmediate);
            projectile.Hit += RecordHit;
            projectile.ReturnedToPool += () => projectile.Hit -= RecordHit;
            projectile.Initialize(shooter);
            projectile.Launch(direction, target.transform.position);
            return projectile;
        }

        private void RecordHit(Vector3 point, IDamageable _) => hitAt = point;

        private IEnumerator WaitForHit()
        {
            for (var i = 0; i < 50 && !hitAt.HasValue; i++)
                yield return new WaitForFixedUpdate();
            Assert.IsTrue(hitAt.HasValue, "the projectile actually reaches its target");
            yield return null;
        }

        private static PooledVFX OnlyImpact()
        {
            var effects = Object.FindObjectsByType<PooledVFX>(FindObjectsSortMode.None);
            Assert.AreEqual(1, effects.Length, "one effect for one hit");
            return effects[0];
        }

        private GameObject Track(GameObject go)
        {
            spawned.Add(go);
            return go;
        }
    }
}

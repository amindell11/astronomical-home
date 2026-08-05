using System.Collections;
using System.Collections.Generic;
using Combat;
using Combat.Weapons;
using Damage;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tests.PlayMode
{
    /// <summary>
    /// Combat identity is the entity's rigidbody, not its hierarchy root: ships sharing a parent
    /// (a multi-arena root) must still hit each other while skipping only themselves.
    /// </summary>
    [Category("Weapons")]
    public class ParentedEntityIdentityPlayModeTests : PlayModeWorldFixture
    {
        private const string RippersPrefabPath = "Assets/Prefabs/Weapons/Rippers.prefab";
        private const string RailgunPrefabPath = "Assets/Prefabs/Weapons/Railgun.prefab";

        private readonly List<GameObject> spawned = new();

        [TearDown]
        public override void TearDown()
        {
            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();

            base.TearDown();
        }

        private sealed class TestShooter : MonoBehaviour, IShooter
        {
            public Vector3 Velocity => Vector3.zero;
            public Rigidbody Body => GetComponent<Rigidbody>();
            public Ships.ShipId Id => Ships.ShipId.Invalid;
        }

        private sealed class DamageRecorder : MonoBehaviour, IDamageable
        {
            public float TotalDamage { get; private set; }

            public void TakeDamage(in DamageInfo hit)
            {
                TotalDamage += hit.Amount;
            }
        }

        private Transform CreateArenaRoot()
        {
            var root = new GameObject("ArenaRoot");
            spawned.Add(root);
            return root.transform;
        }

        private GameObject CreateShooterShip(Transform parent)
        {
            var ship = new GameObject("OwnShip") { layer = LayerIds.Ship };
            spawned.Add(ship);
            ship.transform.SetParent(parent, true);
            ship.AddComponent<Rigidbody>().isKinematic = true;
            ship.AddComponent<BoxCollider>().size = Vector3.one * 2f;
            ship.AddComponent<TestShooter>();
            return ship;
        }

        private DamageRecorder CreateEnemy(Transform parent, Vector3 position)
        {
            var go = new GameObject("Enemy") { layer = LayerIds.Ship };
            spawned.Add(go);
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            go.AddComponent<BoxCollider>().size = Vector3.one;
            return go.AddComponent<DamageRecorder>();
        }

        private T MountWeapon<T>(string path, GameObject ship) where T : WeaponComponent
        {
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(prefab, $"Failed to load weapon prefab at {path}");
            var weapon = Object.Instantiate(prefab, ship.transform);
            spawned.Add(weapon.gameObject);
            return weapon;
#else
            Assert.Ignore("Requires Unity Editor assets.");
            return null;
#endif
        }

        [Test]
        public void RailgunBeam_UnderSharedArenaRoot_SkipsSelfAndHitsEnemy()
        {
            var arena = CreateArenaRoot();
            var ship = CreateShooterShip(arena);
            var ownRecorder = ship.AddComponent<DamageRecorder>();
            var enemy = CreateEnemy(arena, ship.transform.position + Vector3.up * 6f);
            var railgun = MountWeapon<Railguns>(RailgunPrefabPath, ship);

            railgun.Charge.Configure(chargeTime: Time.fixedDeltaTime, minChargeToFire: 1f, autoFireAtFull: true);
            railgun.HandleTrigger(pressed: false, held: true, Projectiles);

            Assert.AreEqual(0f, ownRecorder.TotalDamage, 0.001f, "Never hit the ship that fired.");
            Assert.Greater(enemy.TotalDamage, 0f, "A same-parented enemy is not 'self'.");
        }

        [UnityTest]
        public IEnumerator Projectile_UnderSharedArenaRoot_SkipsSelfAndHitsEnemy()
        {
            var arena = CreateArenaRoot();
            var ship = CreateShooterShip(arena);
            var ownRecorder = ship.AddComponent<DamageRecorder>();
            var enemy = CreateEnemy(arena, ship.transform.position + Vector3.up * 5f);
            var ripper = MountWeapon<Rippers>(RippersPrefabPath, ship);

            ripper.HandleTrigger(pressed: false, held: true, Projectiles);

            for (var i = 0; i < 120 && enemy.TotalDamage <= 0f; i++)
                yield return new WaitForFixedUpdate();

            Assert.AreEqual(0f, ownRecorder.TotalDamage, 0.001f, "Never hit the ship that fired.");
            Assert.Greater(enemy.TotalDamage, 0f, "A same-parented enemy is not 'self'.");
        }

        [Test]
        public void LineOfSight_UnderSharedArenaRoot_OccluderIsNotTargetTransparent()
        {
            var arena = CreateArenaRoot();

            var target = new GameObject("TargetShip") { layer = LayerIds.Ship };
            spawned.Add(target);
            target.transform.SetParent(arena, true);
            target.transform.position = Vector3.up * 10f;
            target.AddComponent<BoxCollider>().size = Vector3.one;

            var occluder = new GameObject("Occluder") { layer = LayerIds.Asteroid };
            spawned.Add(occluder);
            occluder.transform.SetParent(arena, true);
            occluder.transform.position = Vector3.up * 5f;
            occluder.AddComponent<BoxCollider>().size = Vector3.one;

            Physics.SyncTransforms();

            Assert.IsFalse(
                LineOfSight.IsClear(Vector3.zero, target.transform.position, target.transform),
                "A same-parented occluder still blocks line of sight to the target.");

            occluder.SetActive(false);
            Physics.SyncTransforms();

            Assert.IsTrue(
                LineOfSight.IsClear(Vector3.zero, target.transform.position, target.transform),
                "Hitting the target's own collider reads as clear line of sight.");
        }
    }
}

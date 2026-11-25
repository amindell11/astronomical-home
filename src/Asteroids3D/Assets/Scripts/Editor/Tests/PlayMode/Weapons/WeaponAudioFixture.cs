#if UNITY_EDITOR && ASTRO_FMOD
using System.Collections.Generic;
using Combat;
using Combat.Weapons;
using NUnit.Framework;
using Ships.Registry;
using Substrate;
using Tests.PlayMode.Audio;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Weapons
{
    /// <summary>
    /// Local FMOD evidence for weapon audio: weapon prefabs mounted under a bare shooter, triggers
    /// driven directly.
    /// </summary>
    public abstract class WeaponAudioFixture : FmodEvidenceFixture
    {
        private sealed class TestShooter : MonoBehaviour, IShooter
        {
            public Vector3 Velocity => Vector3.zero;
            public Rigidbody Body => null;
            public ShipId Id => ShipId.Invalid;
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

        protected T Mount<T>(string prefabPath) where T : WeaponComponent
        {
            var prefab = AssetDatabase.LoadAssetAtPath<T>(prefabPath);
            Assert.IsNotNull(prefab, $"Failed to load weapon prefab at {prefabPath}");
            var ship = new GameObject("Shooter") { layer = LayerIds.Ship };
            spawned.Add(ship);
            ship.transform.rotation = GamePlane.Rotation;
            ship.AddComponent<TestShooter>();
            var weapon = Object.Instantiate(prefab, ship.transform);
            spawned.Add(weapon.gameObject);
            return weapon;
        }

        protected GameObject Target(Vector3 position)
        {
            var go = new GameObject("Target") { layer = LayerIds.Ship };
            spawned.Add(go);
            go.transform.position = position;
            go.AddComponent<BoxCollider>().size = Vector3.one;
            return go;
        }

        protected static Vector3 Ahead(WeaponComponent weapon, float distance) =>
            weapon.firePoint.position + weapon.firePoint.up * distance;

        protected void Hold(WeaponComponent weapon, int steps)
        {
            for (var i = 0; i < steps; i++)
                weapon.HandleTrigger(new WeaponCommand { pressed = i == 0, held = true }, Projectiles);
        }

        protected void Release(WeaponComponent weapon) =>
            weapon.HandleTrigger(new WeaponCommand { pressed = false, held = false }, Projectiles);
    }
}
#endif

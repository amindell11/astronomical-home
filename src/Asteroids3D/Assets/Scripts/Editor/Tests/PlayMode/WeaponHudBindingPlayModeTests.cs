using System.Collections.Generic;
using Combat.Conditions;
using Combat.Projectile;
using Combat.Weapons;
using NUnit.Framework;
using Ships.Weapons;
using Tests.PlayMode.Common;
using UI;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tests.PlayMode
{
    /// <summary>
    /// The HUD binds weapon readouts by condition presence (Heat → heat gauge, Rounds → ammo
    /// display), not by casting slots to concrete weapon classes. These tests cover the binding
    /// lookup across loadouts — including the Ripper, whose prefabs are also validated here.
    /// </summary>
    [Category("Weapons")]
    public class WeaponHudBindingPlayModeTests : PlayModeWorldFixture
    {
        private const string LasersPrefabPath = "Assets/Prefabs/Weapons/Lasers.prefab";
        private const string MissilesPrefabPath = "Assets/Prefabs/Weapons/Missiles.prefab";
        private const string RippersPrefabPath = "Assets/Prefabs/Weapons/Rippers.prefab";
        private const string RipperSlugPrefabPath = "Assets/Prefabs/Weapons/RipperSlug.prefab";

        private readonly List<GameObject> spawned = new();

        [TearDown]
        public override void TearDown()
        {
            // Destroy in-flight projectiles before their shooters (see WeaponCommandDispatch).
            foreach (var proj in Object.FindObjectsByType<ProjectileBase>(FindObjectsSortMode.None))
                Object.DestroyImmediate(proj.gameObject);

            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();

            base.TearDown();
        }

        private static T LoadWeaponPrefab<T>(string path) where T : WeaponComponent
        {
#if UNITY_EDITOR
            var weapon = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(weapon, $"Failed to load weapon prefab at {path}");
            return weapon;
#else
            Assert.Ignore("WeaponHudBindingPlayModeTests requires Unity Editor assets.");
            return null;
#endif
        }

        /// <summary>Builds an armed WeaponsController; Awake instantiates the given mounts.</summary>
        private WeaponsController CreateController(WeaponComponent primary, WeaponComponent secondary)
        {
            var go = new GameObject("WeaponsControllerTest");
            spawned.Add(go);
            go.SetActive(false);
            var controller = go.AddComponent<WeaponsController>();
            controller.primaryMount = primary;
            controller.secondaryMount = secondary;
            go.SetActive(true);
            return controller;
        }

        [Test]
        public void DefaultLoadout_BindsHeatToPrimary_AmmoToSecondary()
        {
            var controller = CreateController(
                LoadWeaponPrefab<Lasers>(LasersPrefabPath),
                LoadWeaponPrefab<Missiles>(MissilesPrefabPath));

            var heat = Overlay.FindCondition<Heat>(controller, out var heatOwner);
            Assert.IsNotNull(heat);
            Assert.AreSame(controller.Primary, heatOwner);

            var rounds = Overlay.FindCondition<Rounds>(controller, out var roundsOwner);
            Assert.IsNotNull(rounds);
            Assert.AreSame(controller.Secondary, roundsOwner);
            Assert.AreEqual(((Missiles)roundsOwner).Targeting, roundsOwner.LockSource,
                "The ammo readout's lock source comes from the weapon that owns the Rounds.");
        }

        [Test]
        public void RipperLoadout_BindsAmmoToPrimary_AndNoHeatGauge()
        {
            var controller = CreateController(
                LoadWeaponPrefab<Rippers>(RippersPrefabPath),
                LoadWeaponPrefab<Missiles>(MissilesPrefabPath));

            Assert.IsNull(Overlay.FindCondition<Heat>(controller, out _),
                "No equipped weapon carries Heat, so the heat gauge must unbind.");

            var rounds = Overlay.FindCondition<Rounds>(controller, out var roundsOwner);
            Assert.IsNotNull(rounds);
            Assert.AreSame(controller.Primary, roundsOwner, "Earlier slot wins when two mounts carry Rounds.");
            Assert.IsInstanceOf<Rippers>(roundsOwner);
            Assert.IsNull(roundsOwner.LockSource, "A ripper has no lock-on; the ammo readout gets no lock source.");
        }

        [Test]
        public void NullOrUnarmedController_BindsNothing()
        {
            Assert.IsNull(Overlay.FindCondition<Heat>(null, out var owner));
            Assert.IsNull(owner);

            var unarmed = CreateController(null, null);
            Assert.IsNull(Overlay.FindCondition<Rounds>(unarmed, out _));
        }

        [Test]
        public void RipperPrefab_CarriesMagazineCooldownAndSlug()
        {
            var rippers = LoadWeaponPrefab<Rippers>(RippersPrefabPath);

            Assert.IsNotNull(rippers.GetComponent<Rounds>(), "Ripper needs a magazine (Rounds).");
            Assert.IsNotNull(rippers.GetComponent<Cooldown>(), "Ripper needs a fire-rate gate (Cooldown).");
            Assert.IsNull(rippers.GetComponent<Heat>(), "Ripper must not overheat.");

            var slug = rippers.projectilePrefab;
            Assert.IsNotNull(slug, "Ripper's projectile prefab must be wired.");
#if UNITY_EDITOR
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<Laser>(RipperSlugPrefabPath), slug);
#endif
        }

        [UnityTest]
        public System.Collections.IEnumerator RipperWeapon_FiresSlug_AndSpendsAmmo()
        {
            var weapon = Object.Instantiate(LoadWeaponPrefab<Rippers>(RippersPrefabPath));
            spawned.Add(weapon.gameObject);

            var fired = 0;
            weapon.OnFire += () => fired++;
            var startingAmmo = weapon.Rounds.AmmoCount;

            var proj = weapon.Fire();

            Assert.IsNotNull(proj, "Ripper failed to fire from a full magazine.");
            Assert.IsInstanceOf<Laser>(proj);
            Assert.AreEqual(1, fired);
            Assert.AreEqual(startingAmmo - 1, weapon.Rounds.AmmoCount);

            yield return new WaitForFixedUpdate();
            var rb = proj.GetComponent<Rigidbody>();
            Assert.AreEqual(weapon.ProjectileSpeed, rb.linearVelocity.magnitude, 0.5f,
                "Slug should fly at the prefab's muzzle speed.");
        }
    }
}

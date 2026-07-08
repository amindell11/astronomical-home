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
    /// The weapon HUD is generated from the equipped loadout: WeaponReadoutBuilder walks each
    /// mounted weapon's conditions and clones the matching widget template per condition
    /// (Heat → heat gauge, Rounds → ammo display). These tests cover the generation across
    /// loadouts — including the Ripper, whose prefabs are also validated here.
    /// </summary>
    [Category("Weapons")]
    public class WeaponHudBindingPlayModeTests : PlayModeWorldFixture
    {
        private const string LasersPrefabPath = "Assets/Prefabs/Weapons/Lasers.prefab";
        private const string MissilesPrefabPath = "Assets/Prefabs/Weapons/Missiles.prefab";
        private const string RippersPrefabPath = "Assets/Prefabs/Weapons/Rippers.prefab";
        private const string RipperSlugPrefabPath = "Assets/Prefabs/Weapons/RipperSlug.prefab";
        private const string OverlayPrefabPath = "Assets/Prefabs/UI/UIOverlay.prefab";

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

        /// <summary>A builder with bare widget templates, mirroring the HUD panel's setup.</summary>
        private WeaponReadoutBuilder CreateBuilder()
        {
            var panel = new GameObject("HUDPanelTest");
            spawned.Add(panel);
            panel.SetActive(false);

            var heatTemplate = new GameObject("HeatGaugeTemplate").AddComponent<LaserHeatUI>();
            heatTemplate.transform.SetParent(panel.transform);
            var ammoTemplate = new GameObject("AmmoDisplayTemplate").AddComponent<MissileAmmoUI>();
            ammoTemplate.transform.SetParent(panel.transform);

            var builder = panel.AddComponent<WeaponReadoutBuilder>();
            builder.heatTemplate = heatTemplate;
            builder.ammoTemplate = ammoTemplate;
            panel.SetActive(true);
            return builder;
        }

        [Test]
        public void DefaultLoadout_BuildsHeatGaugeAndAmmoDisplay()
        {
            var controller = CreateController(
                LoadWeaponPrefab<Lasers>(LasersPrefabPath),
                LoadWeaponPrefab<Missiles>(MissilesPrefabPath));
            var builder = CreateBuilder();

            builder.Build(controller);

            Assert.AreEqual(2, builder.Built.Count);

            var heatReadout = builder.Built[0];
            Assert.AreSame(controller.Primary, heatReadout.Weapon);
            Assert.IsInstanceOf<Heat>(heatReadout.Condition);
            Assert.IsInstanceOf<LaserHeatUI>(heatReadout.Widget);
            Assert.IsTrue(heatReadout.Widget.gameObject.activeSelf);

            var ammoReadout = builder.Built[1];
            Assert.AreSame(controller.Secondary, ammoReadout.Weapon);
            Assert.IsInstanceOf<Rounds>(ammoReadout.Condition);
            Assert.IsInstanceOf<MissileAmmoUI>(ammoReadout.Widget);

            Assert.IsFalse(builder.heatTemplate.gameObject.activeSelf, "Templates stay hidden.");
            Assert.IsFalse(builder.ammoTemplate.gameObject.activeSelf, "Templates stay hidden.");

            Assert.IsNotNull(builder.FirstCondition<Heat>());
        }

        [Test]
        public void RipperLoadout_BuildsTwoAmmoDisplays_AndNoHeatGauge()
        {
            var controller = CreateController(
                LoadWeaponPrefab<Rippers>(RippersPrefabPath),
                LoadWeaponPrefab<Missiles>(MissilesPrefabPath));
            var builder = CreateBuilder();

            builder.Build(controller);

            // The HUD follows the loadout: one ammo display per Rounds weapon, no heat gauge.
            Assert.AreEqual(2, builder.Built.Count);
            Assert.IsNull(builder.FirstCondition<Heat>());

            Assert.AreSame(controller.Primary, builder.Built[0].Weapon);
            Assert.IsInstanceOf<Rippers>(builder.Built[0].Weapon);
            Assert.IsInstanceOf<Rounds>(builder.Built[0].Condition);

            Assert.AreSame(controller.Secondary, builder.Built[1].Weapon);
            Assert.IsInstanceOf<Missiles>(builder.Built[1].Weapon);
            Assert.IsInstanceOf<Rounds>(builder.Built[1].Condition);
        }

        [Test]
        public void Rebuild_ReplacesPreviousWidgets()
        {
            var controller = CreateController(
                LoadWeaponPrefab<Lasers>(LasersPrefabPath),
                LoadWeaponPrefab<Missiles>(MissilesPrefabPath));
            var builder = CreateBuilder();

            builder.Build(controller);
            var firstWidgets = new List<MonoBehaviour>();
            foreach (var readout in builder.Built)
                firstWidgets.Add(readout.Widget);

            builder.Build(controller);

            Assert.AreEqual(2, builder.Built.Count, "Rebuild must not accumulate widgets.");
            foreach (var widget in firstWidgets)
                Assert.IsFalse(builder.Built[0].Widget == widget || builder.Built[1].Widget == widget,
                    "Rebuild replaces prior widget instances.");
        }

        [Test]
        public void NullOrUnarmedController_BuildsNothing()
        {
            var builder = CreateBuilder();

            builder.Build(null);
            Assert.AreEqual(0, builder.Built.Count);

            var unarmed = CreateController(null, null);
            builder.Build(unarmed);
            Assert.AreEqual(0, builder.Built.Count);
        }

        [Test]
        public void OverlayPrefab_WiresReadoutBuilderTemplates()
        {
#if UNITY_EDITOR
            var overlay = AssetDatabase.LoadAssetAtPath<GameObject>(OverlayPrefabPath);
            Assert.IsNotNull(overlay, "Failed to load UIOverlay prefab");

            var builder = overlay.GetComponentInChildren<WeaponReadoutBuilder>(true);
            Assert.IsNotNull(builder, "UIOverlay must carry a WeaponReadoutBuilder.");
            Assert.IsNotNull(builder.heatTemplate, "Heat gauge template must be wired.");
            Assert.IsNotNull(builder.ammoTemplate, "Ammo display template must be wired.");
            Assert.AreSame(builder.transform, builder.heatTemplate.transform.parent,
                "Templates live in the builder's layout panel so clones inherit its layout.");
#else
            Assert.Ignore("Requires Unity Editor assets.");
#endif
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

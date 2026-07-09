using System.Collections.Generic;
using Combat.Conditions;
using Combat.Projectile;
using Combat.Targeting;
using Combat.Weapons;
using NUnit.Framework;
using Ships.Command;
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
    /// The weapon HUD is generated from the equipped loadout via the IWeaponReadouts read
    /// surface: WeaponReadoutBuilder creates one panel per slot and one widget per readout the
    /// weapon exposes (heat gauge, ammo counter, lock spinner). These tests cover the
    /// generation across loadouts — including the Ripper, whose prefabs are also validated.
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

        /// <summary>Builds an armed controller; Awake instantiates the mounts, Initialize builds the context.</summary>
        private WeaponsController CreateController(WeaponComponent primary, WeaponComponent secondary)
        {
            var go = new GameObject("WeaponsControllerTest");
            spawned.Add(go);
            go.SetActive(false);
            var controller = go.AddComponent<WeaponsController>();
            controller.primaryMount = primary;
            controller.secondaryMount = secondary;
            go.SetActive(true);
            controller.Initialize(() => default);
            return controller;
        }

        /// <summary>A builder wired with bare widget templates, mirroring the HUD panel's setup.</summary>
        private WeaponReadoutBuilder CreateBuilder()
        {
            var root = new GameObject("HUDPanelTest");
            spawned.Add(root);

            var builder = root.AddComponent<WeaponReadoutBuilder>();
            builder.panelPrefab = new GameObject("PanelTemplate").AddComponent<WeaponReadoutPanel>();
            builder.heatWidgetPrefab = new GameObject("HeatTemplate").AddComponent<HeatGaugeUI>();
            builder.ammoWidgetPrefab = new GameObject("AmmoTemplate").AddComponent<AmmoCounterUI>();
            builder.lockWidgetPrefab = new GameObject("LockTemplate").AddComponent<LockReadoutUI>();
            spawned.Add(builder.panelPrefab.gameObject);
            spawned.Add(builder.heatWidgetPrefab.gameObject);
            spawned.Add(builder.ammoWidgetPrefab.gameObject);
            spawned.Add(builder.lockWidgetPrefab.gameObject);
            return builder;
        }

        [Test]
        public void DefaultLoadout_BuildsPanelPerSlot_WidgetPerReadout()
        {
            var controller = CreateController(
                LoadWeaponPrefab<Lasers>(LasersPrefabPath),
                LoadWeaponPrefab<Missiles>(MissilesPrefabPath));
            var builder = CreateBuilder();

            builder.Build(controller.ReadoutContext);

            Assert.AreEqual(2, builder.Panels.Count, "One panel per equipped slot.");

            // Primary (laser): heat gauge. Secondary (missiles): ammo counter + lock spinner.
            Assert.AreEqual(3, builder.Built.Count);

            Assert.AreEqual(WeaponSlot.Primary, builder.Built[0].Slot);
            Assert.IsInstanceOf<IHeatReadout>(builder.Built[0].Readout);
            Assert.IsInstanceOf<HeatGaugeUI>(builder.Built[0].Widget);

            Assert.AreEqual(WeaponSlot.Secondary, builder.Built[1].Slot);
            Assert.IsInstanceOf<IAmmoReadout>(builder.Built[1].Readout);
            Assert.IsInstanceOf<AmmoCounterUI>(builder.Built[1].Widget);

            Assert.AreEqual(WeaponSlot.Secondary, builder.Built[2].Slot);
            Assert.IsInstanceOf<ILockStateSource>(builder.Built[2].Readout);
            Assert.IsInstanceOf<LockReadoutUI>(builder.Built[2].Widget);

            // Widgets live inside their slot's panel, which lives under the builder.
            Assert.AreSame(builder.Panels[0].Container, builder.Built[0].Widget.transform.parent);
            Assert.AreSame(builder.transform, builder.Panels[0].transform.parent);

            Assert.IsNotNull(builder.FirstReadout<IHeatReadout>());
            Assert.IsNotNull(builder.FirstReadout<ILockStateSource>());
        }

        [Test]
        public void RipperLoadout_BuildsTwoAmmoCounters_AndNoHeatGauge()
        {
            var controller = CreateController(
                LoadWeaponPrefab<Rippers>(RippersPrefabPath),
                LoadWeaponPrefab<Missiles>(MissilesPrefabPath));
            var builder = CreateBuilder();

            builder.Build(controller.ReadoutContext);

            // The HUD follows the loadout: each Rounds weapon gets its own ammo counter.
            Assert.AreEqual(2, builder.Panels.Count);
            Assert.IsNull(builder.FirstReadout<IHeatReadout>());

            Assert.AreEqual(WeaponSlot.Primary, builder.Built[0].Slot);
            Assert.IsInstanceOf<IAmmoReadout>(builder.Built[0].Readout);

            Assert.AreEqual(WeaponSlot.Secondary, builder.Built[1].Slot);
            Assert.IsInstanceOf<IAmmoReadout>(builder.Built[1].Readout);
            Assert.AreNotSame(builder.Built[0].Readout, builder.Built[1].Readout);
        }

        [Test]
        public void DisplayName_FallsBackToPrefabName()
        {
            var controller = CreateController(
                LoadWeaponPrefab<Lasers>(LasersPrefabPath),
                LoadWeaponPrefab<Missiles>(MissilesPrefabPath));

            Assert.AreEqual("Lasers", controller.ReadoutContext.DisplayName(WeaponSlot.Primary),
                "DisplayName strips the (Clone) suffix from the instantiated mount.");
        }

        [Test]
        public void Rebuild_ReplacesPreviousPanels()
        {
            var controller = CreateController(
                LoadWeaponPrefab<Lasers>(LasersPrefabPath),
                LoadWeaponPrefab<Missiles>(MissilesPrefabPath));
            var builder = CreateBuilder();

            builder.Build(controller.ReadoutContext);
            var firstPanels = new List<WeaponReadoutPanel>(builder.Panels);

            builder.Build(controller.ReadoutContext);

            Assert.AreEqual(2, builder.Panels.Count, "Rebuild must not accumulate panels.");
            foreach (var panel in builder.Panels)
                Assert.IsFalse(firstPanels.Contains(panel), "Rebuild replaces prior panel instances.");
        }

        [Test]
        public void NullOrUnarmedContext_BuildsNothing()
        {
            var builder = CreateBuilder();

            builder.Build(null);
            Assert.AreEqual(0, builder.Panels.Count);
            Assert.AreEqual(0, builder.Built.Count);

            var unarmed = CreateController(null, null);
            builder.Build(unarmed.ReadoutContext);
            Assert.AreEqual(0, builder.Panels.Count);
        }

        [Test]
        public void OverlayPrefab_WiresReadoutBuilderPrefabs()
        {
#if UNITY_EDITOR
            var overlay = AssetDatabase.LoadAssetAtPath<GameObject>(OverlayPrefabPath);
            Assert.IsNotNull(overlay, "Failed to load UIOverlay prefab");

            var builder = overlay.GetComponentInChildren<WeaponReadoutBuilder>(true);
            Assert.IsNotNull(builder, "UIOverlay must carry a WeaponReadoutBuilder.");
            Assert.IsNotNull(builder.panelPrefab, "Panel prefab must be wired.");
            Assert.IsNotNull(builder.heatWidgetPrefab, "Heat gauge prefab must be wired.");
            Assert.IsNotNull(builder.ammoWidgetPrefab, "Ammo counter prefab must be wired.");
            Assert.IsNotNull(builder.chargeWidgetPrefab, "Charge gauge prefab must be wired.");
            Assert.IsNotNull(builder.lockWidgetPrefab, "Lock readout prefab must be wired.");
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

using System.Collections;
using Combat.Weapons;
using Combat.Weapons.Conditions;
using Combat.Weapons.Mods;
using Loot;
using NUnit.Framework;
using Ships.Weapons;
using Tests.PlayMode.Common;
using UI.Weapons;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tests.PlayMode.Crosshairs
{
    [Category("Weapons")]
    public sealed class WeaponResourceHudPlayModeTests : PlayModeWorldFixture
    {
        private GameObject root;
        private WeaponResourceHud hud;
        private WeaponReadoutBuilder builder;

        [SetUp]
        public void CreateHud()
        {
            root = new GameObject("Weapon HUD test", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            hud = Object.Instantiate(LoadPrefab<WeaponResourceHud>("UI/Crosshair/WeaponKeystoneHud"), root.transform);
            builder = root.AddComponent<WeaponReadoutBuilder>();
            var authoredBuilder = LoadPrefab<WeaponReadoutBuilder>("UI/UIOverlay");
            builder.panelPrefab = authoredBuilder.panelPrefab;
            builder.lockWidgetPrefab = authoredBuilder.lockWidgetPrefab;
        }

        [TearDown]
        public override void TearDown()
        {
            Object.DestroyImmediate(root);
            base.TearDown();
        }

        private static T LoadPrefab<T>(string path) where T : Component
        {
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + path + ".prefab");
            Assert.IsNotNull(prefab);
            return prefab.GetComponentInChildren<T>(true);
#else
            Assert.Ignore("Requires authored weapon and HUD prefabs.");
            return null;
#endif
        }

        private WeaponsController Bind(string weaponName)
        {
            var mounts = new GameObject("Mounts");
            mounts.transform.SetParent(root.transform);
            mounts.SetActive(false);
            var controller = mounts.AddComponent<WeaponsController>();
            controller.primaryMount = LoadPrefab<WeaponComponent>("Weapons/" + weaponName);
            controller.secondaryMount = controller.primaryMount;
            mounts.SetActive(true);
            controller.Initialize(() => default);
            Rebind(controller);
            return controller;
        }

        private void Rebind(WeaponsController controller)
        {
            hud.Initialize(controller.ReadoutContext);
            hud.gameObject.SetActive(true);
            builder.Build(controller.ReadoutContext);
            Draw();
        }

        private void Draw() => hud.SendMessage("LateUpdate");
        private Image ImageAt(string path) => hud.transform.Find(path).GetComponent<Image>();

        [TestCase("Lasers")]
        [TestCase("TurboLasers")]
        [TestCase("Railgun")]
        [TestCase("Rippers")]
        [TestCase("Missiles")]
        [TestCase("Grenades")]
        public void AuthoredWeapon_HasPersistentResourcesInBothPositions(string weaponName)
        {
            Bind(weaponName);
            Assert.IsTrue(hud.transform.Find("PrimaryResources").gameObject.activeInHierarchy);
            Assert.IsTrue(hud.transform.Find("SecondaryResources").gameObject.activeInHierarchy);
            Assert.IsNull(hud.transform.Find("PrimaryResources/ReloadFill"));
            Assert.IsNull(hud.transform.Find("SecondaryResources/ReloadFill"));
            Assert.That(builder.Panels.Count, Is.EqualTo(weaponName == "Missiles" ? 2 : 0));
        }

        [UnityTest]
        public IEnumerator HeatLockout_PulsesCoralWhileCooling_ThenRestoresInk()
        {
            var controller = Bind("Lasers");
            var heat = controller.Primary.GetComponent<Heat>();
            heat.enabled = false;
            heat.Configure(100f, 100f, 20f, 0f, 0f);
            var fill = ImageAt("PrimaryResources/Fill");
            var span = ImageAt("PrimaryResources/Track").fillAmount;
            var ink = fill.color;
            heat.ProcessFire();
            heat.Tick(1f);
            Draw();
            Assert.That(heat.Overheated, Is.True);
            Assert.That(fill.fillAmount, Is.EqualTo(span * .8f).Within(.001f));

            var minimum = 1f;
            var maximum = 0f;
            var until = Time.realtimeSinceStartup + .8f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                Draw();
                var color = fill.color;
                Assert.That(color.g / color.r, Is.EqualTo(.41f / .98f).Within(.001f));
                Assert.That(color.b / color.r, Is.EqualTo(.41f / .98f).Within(.001f));
                Assert.That(color.a, Is.EqualTo(1f));
                Assert.That(ImageAt("SecondaryResources/Fill").color, Is.EqualTo(ink));
                minimum = Mathf.Min(minimum, color.r);
                maximum = Mathf.Max(maximum, color.r);
            }
            Assert.That(maximum - minimum, Is.GreaterThan(.2f));
            Assert.That(minimum, Is.GreaterThanOrEqualTo(.98f * .65f - .001f));
            Assert.That(maximum, Is.LessThanOrEqualTo(.98f + .001f));

            heat.Tick(5f);
            Draw();
            Assert.That(heat.Overheated, Is.False);
            Assert.That(fill.fillAmount, Is.Zero);
            Assert.That(fill.color, Is.EqualTo(ink));
            yield return null;
            Draw();
            Assert.That(fill.color, Is.EqualTo(ink));
        }

        [TestCase("Railgun")]
        public void HeldCharge_FillsTheWeaponArc_AndFiringClearsIt(string weaponName)
        {
            var controller = Bind(weaponName);
            var charge = controller.Primary.GetComponent<ChargeTime>();
            var fill = ImageAt("PrimaryResources/Fill");
            var track = ImageAt("PrimaryResources/Track");
            Assert.That(fill.fillAmount, Is.Zero);
            charge.HandleTrigger(true, charge.FullChargeTime * .5f);
            Draw();
            Assert.That(fill.fillAmount, Is.EqualTo(track.fillAmount * .5f).Within(.001f));
            Assert.That(ImageAt("SecondaryResources/Fill").fillAmount, Is.Zero);
            charge.HandleTrigger(true, charge.FullChargeTime * .5f);
            Draw();
            Assert.That(fill.fillAmount, Is.EqualTo(track.fillAmount).Within(.001f));
            charge.ProcessFire();
            Draw();
            Assert.That(fill.fillAmount, Is.Zero);
            Assert.IsTrue(track.isActiveAndEnabled);
            Assert.IsEmpty(builder.Panels);
            Assert.AreSame(charge, builder.FirstReadout<IChargeReadout>());
        }

        [TestCase("Missiles", 2)]
        [TestCase("Grenades", 3)]
        public void AuthoredLaunchers_UseTenSecondSharedRecharge(string weaponName, int capacity)
        {
            var controller = Bind(weaponName);
            var rounds = controller.Primary.GetComponent<Rounds>();
            Assert.That(rounds.Refill, Is.EqualTo(Rounds.RefillMode.Sequential));
            Assert.That(rounds.ReloadTime, Is.EqualTo(10f));
            Assert.That(rounds.MaxAmmo, Is.EqualTo(capacity));
            for (var shot = 0; shot < capacity; shot++)
            {
                rounds.ProcessFire();
                rounds.Tick(1f);
            }
            Assert.That(rounds.AmmoCount, Is.Zero);
            Assert.That(rounds.ReloadProgress, Is.EqualTo(capacity / 10f).Within(.001f));
            Draw();
            Assert.That(ImageAt("PrimaryResources/Fill").fillAmount, Is.EqualTo(1f / 30f).Within(.001f));
            for (var threshold = 1; threshold < capacity; threshold++)
            {
                Assert.That(ImageAt("PrimaryResources/Threshold" + threshold).fillAmount, Is.EqualTo(2f / 360f));
                Assert.That(ImageAt("SecondaryResources/Threshold" + threshold).fillAmount, Is.EqualTo(2f / 360f));
            }
            Assert.IsNull(hud.transform.Find("PrimaryResources/Round0"));
            rounds.Tick(9.9f - capacity);
            Assert.That(rounds.AmmoCount, Is.Zero);
            rounds.Tick(.1f);
            for (var loaded = 1; loaded <= capacity; loaded++)
            {
                if (loaded > 1) rounds.Tick(10f);
                Draw();
                Assert.That(rounds.AmmoCount, Is.EqualTo(loaded));
                Assert.That(rounds.IsReloading, Is.EqualTo(loaded < capacity));
                Assert.That(ImageAt("PrimaryResources/Fill").fillAmount,
                    Is.EqualTo(loaded / (3f * capacity)).Within(.001f));
            }
        }

        [UnityTest]
        public IEnumerator MissileCapacityUpgrade_AddsAThreshold_AndPreservesSharedRecharge()
        {
            var controller = Bind("Missiles");
            var rounds = controller.Primary.GetComponent<Rounds>();
            rounds.enabled = false;
            rounds.ProcessFire();
            rounds.Tick(5f);
            var mod = ScriptableObject.CreateInstance<WeaponMod>();
            try
            {
                mod.Configure("Extra missile", WeaponModField.Magazine, 7f);
                var stack = new LullModStack();
                stack.Add(mod);
                controller.SetMods(stack);
                Draw();
                yield return null;
                Assert.That(rounds.AmmoCount, Is.EqualTo(8));
                Assert.That(rounds.MaxAmmo, Is.EqualTo(9));
                Assert.That(rounds.ReloadProgress, Is.EqualTo(.5f));
                Assert.IsNotNull(hud.transform.Find("PrimaryResources/Threshold8"));
                var first = ImageAt("PrimaryResources/Threshold1");
                var second = ImageAt("PrimaryResources/Threshold2");
                Assert.That(first.fillAmount, Is.EqualTo(second.fillAmount));
                Assert.That(Quaternion.Angle(first.transform.localRotation, second.transform.localRotation),
                    Is.EqualTo(120f / 9f).Within(.001f));
                Assert.That(ImageAt("PrimaryResources/Fill").fillAmount, Is.EqualTo(8.5f / 27f).Within(.001f));
                rounds.Tick(5f);
                Assert.That(rounds.AmmoCount, Is.EqualTo(9));
                Assert.IsFalse(rounds.IsReloading);
            }
            finally { Object.DestroyImmediate(mod); }
        }

        [Test]
        public void RipperMagazine_ReloadFillsItsAmmoArcThroughCompletion()
        {
            var controller = Bind("Rippers");
            var rounds = controller.Primary.GetComponent<Rounds>();
            var fill = ImageAt("PrimaryResources/Fill");
            var span = ImageAt("PrimaryResources/Track").fillAmount;
            rounds.ProcessFire();
            Draw();
            Assert.That(fill.fillAmount, Is.EqualTo(span * 23f / 24f).Within(.001f));
            while (rounds.AmmoCount > 0) rounds.ProcessFire();
            Draw();
            Assert.That(fill.fillAmount, Is.Zero);
            rounds.Tick(rounds.ReloadTime * .5f);
            Draw();
            Assert.That(fill.fillAmount, Is.EqualTo(span * .5f).Within(.001f));
            rounds.Tick(rounds.ReloadTime * .5f);
            Draw();
            Assert.That(fill.fillAmount, Is.EqualTo(span).Within(.001f));
            Assert.IsNull(hud.transform.Find("PrimaryResources/ReloadFill"));
        }

        [UnityTest]
        public IEnumerator WeaponSwap_ReplacesAmmoSegmentsWithCharge_AndClearsTheEmptyPosition()
        {
            var controller = Bind("Grenades");
            var oldResources = hud.transform.Find("PrimaryResources").gameObject;
            controller.Reequip(LoadPrefab<WeaponComponent>("Weapons/Railgun"), null);
            Rebind(controller);
            Assert.IsFalse(oldResources.activeSelf);
            yield return null;
            Assert.IsNull(hud.transform.Find("SecondaryResources"));
            Assert.IsNull(hud.transform.Find("PrimaryResources/Round0"));
            var charge = controller.Primary.GetComponent<ChargeTime>();
            charge.HandleTrigger(true, charge.FullChargeTime * .5f);
            Draw();
            Assert.That(ImageAt("PrimaryResources/Fill").fillAmount, Is.EqualTo(1f / 6f).Within(.001f));
            Assert.IsEmpty(builder.Panels);
        }
    }
}

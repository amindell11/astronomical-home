#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Balance;
using Combat.Projectiles;
using Combat.Weapons;
using Combat.Weapons.Conditions;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// The probe fires real weapons: made-up weapons check what it reports, and every shipped
    /// weapon prefab must yield modes. Timings allow one fixed step per timed event until #803.
    /// </summary>
    [Category("Weapons")]
    public class WeaponCycleProbePlayModeTests : PlayModeWorldFixture
    {
        private const string WeaponPrefabFolder = "Assets/Prefabs/Weapons";

        private readonly List<GameObject> spawned = new();

        [TearDown]
        public override void TearDown()
        {
            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();
            base.TearDown();
        }

        private static float Steps(int count) => count * Time.fixedDeltaTime + 0.001f;

        private static void SetFloat(Object target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Inactive, so nothing on it wakes: the probe's instance is the one that runs.
        private T Template<T>() where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            go.SetActive(false);
            spawned.Add(go);
            return go.AddComponent<T>();
        }

        private Laser Bolt(float damage)
        {
            var bolt = Template<Laser>();
            bolt.gameObject.AddComponent<Rigidbody>().useGravity = false;
            SetFloat(bolt, "damage", damage);
            return bolt;
        }

        private static void AddCooldown(Component weapon, float secondsBetweenShots) =>
            SetFloat(weapon.gameObject.AddComponent<Cooldown>(), "fireRate", secondsBetweenShots);

        private IEnumerator Measure(WeaponComponent weapon, List<WeaponCycleMode> modes)
        {
            yield return WeaponCycleProbe.Measure(weapon, Projectiles, modes);
            foreach (var mode in modes)
                TestContext.WriteLine(Row(weapon.DisplayName, mode));
        }

        private static string Row(string weapon, WeaponCycleMode mode) =>
            $"{weapon} / {mode.Label}: opening {mode.OpeningDamage:0.##} over {mode.OpeningSeconds:0.###} s; " +
            $"magazine {mode.MagazineDamage:0.##}, dump {mode.DumpSeconds:0.###} s, recovery {mode.RecoverySeconds:0.###} s, " +
            $"cycle {mode.CycleSeconds:0.###} s, sustained {mode.SustainedDps:0.###} DPS";

        [UnityTest]
        public IEnumerator MagazineWeapon_ReportsMagazineDumpAndReload()
        {
            var rippers = Template<Rippers>();
            rippers.projectilePrefab = Bolt(damage: 4f);
            AddCooldown(rippers, secondsBetweenShots: 0.2f);
            rippers.gameObject.AddComponent<Rounds>().Configure(maxAmmo: 4, reloadTime: 1f);

            var modes = new List<WeaponCycleMode>();
            yield return Measure(rippers, modes);

            Assert.AreEqual(1, modes.Count, "the AI fires a magazine weapon like a held trigger");
            var hold = modes[0];
            Assert.AreEqual("hold", hold.Label);
            Assert.AreEqual(16f, hold.OpeningDamage, 0.001f);
            Assert.AreEqual(16f, hold.MagazineDamage, 0.001f);
            Assert.AreEqual(0.6f, hold.DumpSeconds, Steps(3));
            Assert.AreEqual(1f, hold.RecoverySeconds, Steps(2));
        }

        [UnityTest]
        public IEnumerator HeatWeapon_AiHoldsBackTheOverheatingShot()
        {
            var lasers = Template<Lasers>();
            lasers.projectilePrefab = Bolt(damage: 5f);
            AddCooldown(lasers, secondsBetweenShots: 0.1f);
            lasers.gameObject.AddComponent<Heat>().Configure(maxHeat: 100f, heatPerShot: 30f, coolingRate: 100f,
                coolDownDelay: 0.1f, overheatPenaltyTime: 0.5f);

            var modes = new List<WeaponCycleMode>();
            yield return Measure(lasers, modes);

            CollectionAssert.AreEqual(new[] { "hold", "AI" }, modes.Select(m => m.Label).ToArray());
            Assert.AreEqual(20f, modes[0].OpeningDamage, 0.001f, "held: 30, 60, 90, then the overheating fourth shot");
            Assert.AreEqual(20f, modes[0].MagazineDamage, 0.001f);
            Assert.AreEqual(15f, modes[1].OpeningDamage, 0.001f, "the AI stops at 90 heat, short of overheating");
            Assert.AreEqual(5f, modes[1].MagazineDamage, 0.001f, "then fires one shot each time heat drops under 70");
        }

        [UnityTest]
        public IEnumerator ChargeWeapon_TapFiresAtMinimumCharge()
        {
            var lasers = Template<ChargeLasers>();
            lasers.projectilePrefab = Bolt(damage: 20f);
            SetFloat(lasers, "minChargeDamageScale", 0.5f);
            SetFloat(lasers, "fullChargeDamageScale", 1f);
            AddCooldown(lasers, secondsBetweenShots: 0.1f);
            lasers.gameObject.AddComponent<ChargeTime>().Configure(chargeTime: 1f, minChargeToFire: 0.5f);

            var modes = new List<WeaponCycleMode>();
            yield return Measure(lasers, modes);

            CollectionAssert.AreEqual(new[] { "hold", "tap" }, modes.Select(m => m.Label).ToArray());
            Assert.AreEqual(20f, modes[0].MagazineDamage, 0.001f, "held to full charge");
            Assert.AreEqual(1f, modes[0].CycleSeconds, Steps(2));
            Assert.AreEqual(15f, modes[1].MagazineDamage, 0.5f, "released at half charge: halfway from 0.5 to 1 of 20");
            Assert.AreEqual(0.5f, modes[1].CycleSeconds, Steps(2));
        }

        [UnityTest]
        public IEnumerator EveryWeaponPrefab_YieldsFiniteModes()
        {
            var weapons = AssetDatabase.FindAssets("t:Prefab", new[] { WeaponPrefabFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<WeaponComponent>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(weapon => weapon)
                .ToList();
            CollectionAssert.IsSubsetOf(
                new[] { "Lasers", "Rippers", "ChargeLasers", "Railgun", "Missiles", "Grenades" },
                weapons.Select(weapon => weapon.name).ToList(),
                "test premise: the search finds the shipped weapon prefabs");

            foreach (var weapon in weapons)
            {
                var modes = new List<WeaponCycleMode>();
                yield return Measure(weapon, modes);

                Assert.IsNotEmpty(modes, $"{weapon.name} fired no shot under any trigger pattern");
                Assert.AreEqual("hold", modes[0].Label, $"{weapon.name}: the held trigger comes first");
                foreach (var mode in modes)
                {
                    var row = Row(weapon.name, mode);
                    Assert.That(mode.OpeningDamage, Is.GreaterThan(0f).And.LessThan(float.PositiveInfinity), row);
                    Assert.That(mode.MagazineDamage, Is.GreaterThan(0f).And.LessThan(float.PositiveInfinity), row);
                    Assert.That(mode.SustainedDps, Is.GreaterThan(0f).And.LessThan(float.PositiveInfinity), row);
                }
            }
        }
    }
}
#endif

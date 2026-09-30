using System.Collections.Generic;
using System.Linq;
using Combat.Projectiles;
using Combat.Weapons;
using Combat.Weapons.Conditions;
using NUnit.Framework;
using Ships.Loadout;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Weapon cycle math on made-up values, so a retune never trips it, plus the prefab-asset
    /// read every shipped weapon must survive. Weapons are wired through serialized references
    /// only, the way a prefab asset is: EditMode runs no Awake to backfill them.
    /// </summary>
    [Category("Weapons")]
    public class WeaponCycleModeEditModeTests
    {
        private const float Tolerance = 0.0001f;
        private const string WeaponPrefabFolder = "Assets/Prefabs/Weapons";

        private readonly List<GameObject> spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
                if (go) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        private T New<T>() where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            spawned.Add(go);
            return go.AddComponent<T>();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T Wire<T>(WeaponComponent weapon, string field) where T : WeaponCondition
        {
            var condition = weapon.gameObject.AddComponent<T>();
            SetReference(weapon, field, condition);
            return condition;
        }

        private static void WireCooldown(WeaponComponent weapon, float secondsBetweenShots) =>
            SetFloat(Wire<Cooldown>(weapon, "cooldown"), "fireRate", secondsBetweenShots);

        private T NewProjectile<T>(float damage) where T : ProjectileBase
        {
            var projectile = New<T>();
            SetFloat(projectile, "damage", damage);
            return projectile;
        }

        private static void AssertMode(WeaponCycleMode mode, string label, float magazine, float dump, float recovery)
        {
            Assert.AreEqual(label, mode.Label);
            Assert.AreEqual(magazine, mode.MagazineDamage, Tolerance, $"{label} magazine");
            Assert.AreEqual(dump, mode.DumpSeconds, Tolerance, $"{label} dump");
            Assert.AreEqual(recovery, mode.RecoverySeconds, Tolerance, $"{label} recovery");
        }

        [Test]
        public void Mode_DerivesCycleSustainedDpsAndStakes()
        {
            var mode = new WeaponCycleMode("made-up", magazineDamage: 60f, dumpSeconds: 2f, recoverySeconds: 4f);
            var ship = new ResolvedShipStats { maxHealth = 80f, maxShield = 40f };

            Assert.AreEqual(6f, mode.CycleSeconds, Tolerance);
            Assert.AreEqual(10f, mode.SustainedDps, Tolerance);
            Assert.AreEqual(120f, ship.ShipResourcePool, Tolerance);
            Assert.AreEqual(0.5f, mode.Stakes(ship.ShipResourcePool), Tolerance);
        }

        private Lasers NewLasers(float damage, float secondsBetweenShots, float heatPerShot)
        {
            var lasers = New<Lasers>();
            lasers.projectilePrefab = NewProjectile<Laser>(damage);
            WireCooldown(lasers, secondsBetweenShots);
            Wire<Heat>(lasers, "heat").Configure(maxHeat: 100f, heatPerShot: heatPerShot, coolingRate: 40f,
                coolDownDelay: 0.25f, overheatPenaltyTime: 2f);
            return lasers;
        }

        [Test]
        public void Lasers_StepHeatsOwnRule_ForOverheatAndManagedModes()
        {
            // Cooling 10 between shots reads 30, 50, 70, 90, then overheats; 100 / 30 floors to 3.
            var lasers = NewLasers(damage: 7f, secondsBetweenShots: 0.5f, heatPerShot: 30f);

            Assert.AreEqual(5, lasers.ShotsToOverheat);
            var modes = lasers.CycleModes;
            Assert.AreEqual(2, modes.Count);
            AssertMode(modes[0], "overheat", magazine: 35f, dump: 2f, recovery: 2f + 100f / 40f);
            AssertMode(modes[1], "managed", magazine: 28f, dump: 1.5f, recovery: 0.25f + 90f / 40f);
        }

        [Test]
        public void Lasers_WhenCoolingKeepsPace_ReportOneSustainedMode()
        {
            var lasers = NewLasers(damage: 7f, secondsBetweenShots: 0.5f, heatPerShot: 10f);

            Assert.IsNull(lasers.ShotsToOverheat);
            var modes = lasers.CycleModes;
            Assert.AreEqual(1, modes.Count);
            AssertMode(modes[0], "sustained", magazine: 7f, dump: 0.5f, recovery: 0f);
        }

        [Test]
        public void Rippers_ReloadStartsAtTheLastShot()
        {
            var rippers = New<Rippers>();
            rippers.projectilePrefab = NewProjectile<Laser>(damage: 3f);
            WireCooldown(rippers, secondsBetweenShots: 0.25f);
            Wire<Rounds>(rippers, "rounds").Configure(maxAmmo: 10, reloadTime: 2f);

            var mode = rippers.CycleModes.Single();
            AssertMode(mode, "magazine", magazine: 30f, dump: 2.25f, recovery: 2f);
            Assert.AreEqual(30f / 4.25f, mode.SustainedDps, Tolerance);
        }

        // Charge accrues while the cooldown runs, so a tap waits for whichever is longer.
        [TestCase(1f, 1f, 40f, TestName = "ChargeLasers_TapPacedByCooldown")]
        [TestCase(0.2f, 0.5f, 30f, TestName = "ChargeLasers_TapPacedByMinCharge")]
        public void ChargeLasers_ReportFullAndMinCharge(float secondsBetweenShots, float tapSeconds, float tapDamage)
        {
            var lasers = New<ChargeLasers>();
            lasers.projectilePrefab = NewProjectile<Laser>(damage: 40f);
            SetFloat(lasers, "minChargeDamageScale", 0.5f);
            SetFloat(lasers, "fullChargeDamageScale", 1.5f);
            WireCooldown(lasers, secondsBetweenShots);
            Wire<ChargeTime>(lasers, "charge").Configure(chargeTime: 2f, minChargeToFire: 0.25f);

            var modes = lasers.CycleModes;
            Assert.AreEqual(2, modes.Count);
            AssertMode(modes[0], "full charge", magazine: 60f, dump: 2f, recovery: 0f);
            AssertMode(modes[1], "min charge", magazine: tapDamage, dump: tapSeconds, recovery: 0f);
        }

        [Test]
        public void Railguns_RecoverOnlyTheCooldownLeftAfterTheCharge()
        {
            var railguns = New<Railguns>();
            SetFloat(railguns, "damage", 50f);
            WireCooldown(railguns, secondsBetweenShots: 2.5f);
            Wire<ChargeTime>(railguns, "charge").Configure(chargeTime: 1f, minChargeToFire: 1f);

            AssertMode(railguns.CycleModes.Single(), "full charge", magazine: 50f, dump: 1f, recovery: 1.5f);
        }

        [Test]
        public void Missiles_PerRoundRegen_ReplaysTheDumpEveryReloadTime()
        {
            var missiles = New<Missiles>();
            missiles.projectilePrefab = NewProjectile<Missile>(damage: 9f);
            WireCooldown(missiles, secondsBetweenShots: 2f);
            Wire<Rounds>(missiles, "rounds").Configure(maxAmmo: 3, reloadTime: 10f, Rounds.RefillMode.PerRound);

            var mode = missiles.CycleModes.Single();
            AssertMode(mode, "regen", magazine: 27f, dump: 4f, recovery: 6f);
            Assert.AreEqual(10f, mode.CycleSeconds, Tolerance);
        }

        [Test]
        public void Grenades_ThatNeverRefill_RecoverNeverAndSustainNothing()
        {
            var wave = New<ConcussionWave>();
            SetFloat(wave, "maxDamage", 25f);
            var grenade = NewProjectile<Grenade>(damage: 0f);
            SetReference(grenade, "wavePrefab", wave);

            var grenades = New<Grenades>();
            grenades.projectilePrefab = grenade;
            WireCooldown(grenades, secondsBetweenShots: 2f);
            Wire<Rounds>(grenades, "rounds").Configure(maxAmmo: 2, reloadTime: 0f, Rounds.RefillMode.PerRound);

            var mode = grenades.CycleModes.Single();
            Assert.AreEqual("regen", mode.Label);
            Assert.AreEqual(50f, mode.MagazineDamage, Tolerance);
            Assert.AreEqual(2f, mode.DumpSeconds, Tolerance);
            Assert.IsTrue(float.IsPositiveInfinity(mode.RecoverySeconds));
            Assert.AreEqual(0f, mode.SustainedDps);
        }

        // Read off the prefab asset, where Awake never runs: an unwired condition reference throws here.
        [Test]
        public void EveryWeaponPrefab_YieldsFiniteModesOffTheAsset()
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
                var modes = weapon.CycleModes;
                Assert.IsNotEmpty(modes, $"{weapon.name} reports no cycle mode");
                foreach (var mode in modes)
                {
                    var row = $"{weapon.name} / {mode.Label}: magazine {mode.MagazineDamage:0.###}, dump {mode.DumpSeconds:0.###} s, " +
                              $"recovery {mode.RecoverySeconds:0.###} s, cycle {mode.CycleSeconds:0.###} s, sustained {mode.SustainedDps:0.###} DPS";
                    TestContext.WriteLine(row);
                    Assert.That(mode.MagazineDamage, Is.GreaterThan(0f).And.LessThan(float.PositiveInfinity), row);
                    Assert.That(mode.CycleSeconds, Is.GreaterThan(0f).And.LessThan(float.PositiveInfinity), row);
                    Assert.That(mode.SustainedDps, Is.GreaterThan(0f).And.LessThan(float.PositiveInfinity), row);
                }
            }
        }
    }
}

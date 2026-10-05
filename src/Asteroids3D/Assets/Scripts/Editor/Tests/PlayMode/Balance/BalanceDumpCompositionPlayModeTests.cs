#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Balance;
using Combat.Projectiles;
using Combat.Weapons.Arsenal;
using Combat.Weapons.Conditions;
using Game.Runs;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Balance
{
    [Category("Weapons")]
    public class BalanceDumpCompositionPlayModeTests : PlayModeWorldFixture
    {
        private readonly List<Object> spawned = new();

        [TearDown]
        public override void TearDown()
        {
            foreach (var item in spawned)
                if (item) Object.DestroyImmediate(item);
            spawned.Clear();
            base.TearDown();
        }

        // Inactive, so nothing on it wakes: the probe's instance is the one that runs.
        private T Template<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.SetActive(false);
            spawned.Add(go);
            return go.AddComponent<T>();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private Laser Bolt(string name, float damage)
        {
            var bolt = Template<Laser>(name);
            bolt.gameObject.AddComponent<Rigidbody>().useGravity = false;
            SetFloat(bolt, "damage", damage);
            return bolt;
        }

        private Ship Hull(string name, float maxHealth)
        {
            var hull = Template<Ship>(name);
            hull.maxHealth = maxHealth;
            return hull;
        }

        private ShieldModule Shield(string name, float maxShield)
        {
            var shield = ScriptableObject.CreateInstance<ShieldModule>();
            shield.name = name;
            shield.maxShield = maxShield;
            spawned.Add(shield);
            return shield;
        }

        private ItemCatalog Catalog(Ship[] chassis, ShieldModule[] shields, Component[] weapons)
        {
            var catalog = ScriptableObject.CreateInstance<ItemCatalog>();
            spawned.Add(catalog);
            var serialized = new SerializedObject(catalog);
            void Fill(string field, Object[] items)
            {
                var list = serialized.FindProperty(field);
                list.arraySize = items.Length;
                for (var i = 0; i < items.Length; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
            Fill("chassis", chassis);
            Fill("engines", Array.Empty<Object>());
            Fill("shields", shields);
            Fill("weapons", weapons);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        [UnityTest]
        public IEnumerator Measure_ComposesRowsPoolsAndInputs()
        {
            var lasers = Template<Lasers>("MadeUpLasers");
            lasers.projectilePrefab = Bolt("MadeUpBolt", damage: 5f);
            SetFloat(lasers.gameObject.AddComponent<Cooldown>(), "fireRate", 0.1f);
            lasers.gameObject.AddComponent<Heat>().Configure(maxHeat: 100f, heatPerShot: 30f, coolingRate: 100f,
                coolDownDelay: 0.1f, overheatPenaltyTime: 0.5f);
            var rippers = Template<Rippers>("MadeUpRippers");
            rippers.projectilePrefab = Bolt("MadeUpSlug", damage: 4f);
            SetFloat(rippers.gameObject.AddComponent<Cooldown>(), "fireRate", 0.2f);
            rippers.gameObject.AddComponent<Rounds>().Configure(maxAmmo: 4, reloadTime: 1f);
            var catalog = Catalog(
                new[] { Hull("LightHull", maxHealth: 100f), Hull("HeavyHull", maxHealth: 150f) },
                new[] { Shield("ThinShield", maxShield: 50f), Shield("ThickShield", maxShield: 100f) },
                new Component[] { lasers, rippers });

            var measured = default(BalanceDump);
            yield return BalanceDump.Measure(catalog, Projectiles, new DateTime(2026, 10, 4, 12, 30, 5, DateTimeKind.Utc),
                new BuildIdentity { commit = "0123abcd", dirty = true }, "f0e1d2c3b4a59687",
                new[] { "setting/killHullRestore=0.25" }, dump => measured = dump);
            // Assert on what the file carries.
            var dump = JsonUtility.FromJson<BalanceDump>(measured.ToJson());

            Assert.AreEqual("balance-dump-v1", dump.schema);
            Assert.AreEqual("2026-10-04T12:30:05Z", dump.takenUtc);
            Assert.AreEqual("0123abcd", dump.buildIdentity.commit);
            Assert.AreEqual("f0e1d2c3b4a59687", dump.statFingerprint);
            CollectionAssert.AreEqual(new[] { 150f, 200f, 250f }, dump.pools,
                "one ship resource pool per distinct hull plus shield: 100 + 100 and 150 + 50 are one");

            CollectionAssert.AreEqual(new[] { "MadeUpLasers/hold", "MadeUpLasers/AI", "MadeUpRippers/hold" },
                dump.derived.Select(row => $"{row.weapon}/{row.mode}").ToArray(),
                "catalog order, then the probe's mode order");
            var magazine = dump.derived[2];
            Assert.AreEqual(16f, magazine.openingDamage, 0.001f);
            Assert.AreEqual(10f, magazine.sustainedDps, 0.2f, "16 every 0.6 s dump plus 1 s reload");
            Assert.That(magazine.stakes, Is.EqualTo(new[] { 16f / 150f, 16f / 200f, 16f / 250f }).Within(0.001f),
                "the opening burst over each ship resource pool, in pool order");
            Assert.IsTrue(dump.derived.All(row => row.stakes.Count == 3), "every row has a stake per ship resource pool");

            CollectionAssert.IsOrdered(dump.inputs, StringComparer.Ordinal);
            CollectionAssert.AllItemsAreUnique(dump.inputs);
            CollectionAssert.IsSubsetOf(new[]
            {
                "setting/killHullRestore=0.25",
                "HeavyHull/Ship.maxHealth=150",
                "ThickShield/ShieldModule.maxShield=100",
                "MadeUpLasers/Lasers.projectilePrefab=MadeUpBolt",
                "MadeUpBolt/Laser.damage=5",
                "MadeUpSlug/Laser.damage=4",
            }, dump.inputs, "the setting lines plus every catalog item's stat lines");
        }
    }
}
#endif

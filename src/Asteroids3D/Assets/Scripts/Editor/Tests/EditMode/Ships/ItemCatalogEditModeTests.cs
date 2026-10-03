using System.Collections.Generic;
using System.Linq;
using Combat.Weapons;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    [TestFixture]
    [Category("Ships")]
    public class ItemCatalogEditModeTests
    {
        private ItemCatalog catalog;

        [SetUp]
        public void LoadCatalog()
        {
            catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalog.AssetPath);
            Assert.IsNotNull(catalog, $"ItemCatalog missing at {ItemCatalog.AssetPath}");
        }

        [Test]
        public void Chassis_ListEveryPrefabWithARootShip() =>
            AssertGroupMatchesProject("chassis", catalog.Chassis, PrefabsWithRoot<Ship>());

        [Test]
        public void Engines_ListEveryEngineModuleAsset() =>
            AssertGroupMatchesProject("engines", catalog.Engines, AssetsOfType<EngineModule>());

        [Test]
        public void Shields_ListEveryShieldModuleAsset() =>
            AssertGroupMatchesProject("shields", catalog.Shields, AssetsOfType<ShieldModule>());

        [Test]
        public void Weapons_ListEveryPrefabWithARootWeaponComponent() =>
            AssertGroupMatchesProject("weapons", catalog.Weapons, PrefabsWithRoot<WeaponComponent>());

        private static List<string> PrefabsWithRoot<T>() where T : Component =>
            AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<T>())
                .ToList();

        private static List<string> AssetsOfType<T>() where T : ScriptableObject =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToList();

        private static void AssertGroupMatchesProject<T>(string group, IReadOnlyList<T> entries, List<string> scanned)
            where T : Object
        {
            Assert.IsNotEmpty(scanned, $"the project scan found no {group} — the test has lost its subject");

            var listed = entries.Select(e => e ? AssetDatabase.GetAssetPath(e) : "<unset entry>").ToList();
            var missing = scanned.Except(listed).ToList();
            // One removal per scanned path, so an item listed twice stays as extra.
            var extra = listed.ToList();
            foreach (var path in scanned) extra.Remove(path);

            Assert.IsTrue(missing.Count == 0 && extra.Count == 0,
                $"{ItemCatalog.AssetPath} '{group}' differs from the project.\n" +
                $"In the project but not listed: {Format(missing)}\n" +
                $"Listed but not in the project (or listed twice): {Format(extra)}");
        }

        private static string Format(List<string> paths) =>
            paths.Count == 0 ? "none" : "\n  " + string.Join("\n  ", paths);
    }
}

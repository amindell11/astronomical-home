using System.Collections.Generic;
using Combat.Projectiles;
using Combat.Weapons;
using NUnit.Framework;
using Ships.Loadout;
using UnityEditor;

namespace Tests.EditMode.Weapons
{
    [TestFixture]
    [Category("Weapons")]
    public class WeaponWiringEditModeTests
    {
        [Test]
        public void CatalogWeapons_WireTheirProjectileAndTheReferencesHangarStatsRead()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalog.AssetPath);
            Assert.IsNotNull(catalog, $"ItemCatalog missing at {ItemCatalog.AssetPath}");

            var unwired = new List<string>();
            foreach (var weapon in catalog.Weapons)
            {
                var path = AssetDatabase.GetAssetPath(weapon);
                // Null property: not a WeaponBase<TProj>, so it has no projectile to wire.
                var projectile = new SerializedObject(weapon)
                    .FindProperty(nameof(WeaponBase<ProjectileBase>.projectilePrefab));
                if (projectile != null && !projectile.objectReferenceValue)
                    unwired.Add($"{path}: projectilePrefab is unassigned");

                // HangarScreen.Describe reads the blast stats through the grenade's wave.
                var grenades = weapon as Grenades;
                if (grenades && grenades.projectilePrefab && !grenades.projectilePrefab.WavePrefab)
                    unwired.Add($"{path}: its projectile {AssetDatabase.GetAssetPath(grenades.projectilePrefab)} " +
                                "has no wavePrefab (the blast settings)");
            }

            Assert.IsEmpty(unwired, "Weapon prefabs missing a required reference:\n  " + string.Join("\n  ", unwired));
        }
    }
}

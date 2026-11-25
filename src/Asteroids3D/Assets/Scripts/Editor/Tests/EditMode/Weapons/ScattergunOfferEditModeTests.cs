using System.Linq;
using Combat.Weapons;
using NUnit.Framework;
using Ships.Command;
using Ships.Loadout;
using Ships.Weapons;
using UnityEditor;

namespace Tests.EditMode.Weapons
{
    [TestFixture]
    [Category("Weapons")]
    public class ScattergunOfferEditModeTests
    {
        private const string PrefabPath = "Assets/Prefabs/Weapons/Scattergun.prefab";
        private const string PoolPath = "Assets/Settings/Ships/PlayerLoadout.asset";

        [Test]
        public void Offer_ListsScattergunInTheWeaponSlot()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<WeaponComponent>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath);

            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalog.AssetPath);
            Assert.IsNotNull(catalog, ItemCatalog.AssetPath);
            Assert.IsTrue(catalog.Weapons.Any(weapon => weapon == prefab), "ItemCatalog.Weapons");

            var pool = AssetDatabase.LoadAssetAtPath<ItemSubset>(PoolPath);
            Assert.IsNotNull(pool, PoolPath);
            Assert.IsNotNull(pool.weapons, PoolPath);
            Assert.IsTrue(pool.weapons.Any(weapon => weapon == prefab), "PlayerLoadout.weapons");

            Assert.AreEqual(WeaponSlot.Primary, WeaponsController.SlotFor(prefab));
        }
    }
}

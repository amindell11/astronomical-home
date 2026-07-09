#if UNITY_EDITOR
using Combat.Weapons;
using NUnit.Framework;
using Ships;
using Ships.Weapons;
using Tests.PlayMode.Common;
using UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tests.PlayMode
{
    /// <summary>
    /// The hangar's weapon rows: each mount gets its own row built from the shared catalog pool,
    /// a click writes only its own loadout slot, and a ship pick reseeds both mounts to the
    /// ship's authored kit. Headless-safe: the preview RawImage is removed before Show so no
    /// render-texture stage is ever created.
    /// </summary>
    [Category("UI")]
    public class HangarWeaponRowsPlayModeTests : PlayModeWorldFixture
    {
        private const string ScreenPrefabPath = "Assets/Prefabs/UI/HangarScreen.prefab";
        private const string Ship1Path = "Assets/Prefabs/Ships/Ship_1.prefab";

        private static readonly string[] WeaponPaths =
        {
            "Assets/Prefabs/Weapons/Lasers.prefab",
            "Assets/Prefabs/Weapons/ChargeLasers.prefab",
            "Assets/Prefabs/Weapons/Missiles.prefab",
            "Assets/Prefabs/Weapons/Railgun.prefab",
            "Assets/Prefabs/Weapons/Rippers.prefab",
        };

        private HangarScreen screen;
        private LoadoutConfig catalog;

        public override void TearDown()
        {
            DestroyTestObject(screen);
            if (catalog) Object.DestroyImmediate(catalog);
            if (EventSystem.current) DestroyTestObject(EventSystem.current.gameObject);
            base.TearDown();
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"asset loads: {path}");
            return asset;
        }

        private ShipLoadout ShowScreen(out Ship ship1)
        {
            ship1 = Load<Ship>(Ship1Path);

            catalog = ScriptableObject.CreateInstance<LoadoutConfig>();
            catalog.ships = new[] { ship1 };
            catalog.engines = new EngineModule[0];
            catalog.shields = new ShieldModule[0];
            var weapons = new WeaponComponent[WeaponPaths.Length];
            for (var i = 0; i < WeaponPaths.Length; i++)
                weapons[i] = Load<WeaponComponent>(WeaponPaths[i]);
            catalog.weapons = weapons;

            screen = Object.Instantiate(Load<HangarScreen>(ScreenPrefabPath));
            Object.DestroyImmediate(screen.GetComponentInChildren<RawImage>());

            var loadout = new ShipLoadout(ship1, ship1.Engine, ship1.Shield, null, null);
            screen.Show(catalog, loadout, onLaunch: null);
            return loadout;
        }

        private Button[] RowButtons(string rowName)
        {
            var row = screen.transform.Find($"Panel/{rowName}");
            Assert.IsNotNull(row, $"{rowName} exists under Panel");
            return row.GetComponentsInChildren<Button>();
        }

        [Test]
        public void WeaponRows_PopulateFromCatalog_WithDisplayNames()
        {
            ShowScreen(out _);

            foreach (var rowName in new[] { "PrimaryWeaponRow", "SecondaryWeaponRow" })
            {
                var buttons = RowButtons(rowName);
                Assert.AreEqual(catalog.weapons.Length, buttons.Length,
                    $"{rowName} shows one button per catalog weapon");
                for (var i = 0; i < buttons.Length; i++)
                    Assert.AreEqual(catalog.weapons[i].DisplayName,
                        buttons[i].GetComponentInChildren<Text>().text,
                        $"{rowName} button {i} is labeled with the weapon's display name");
            }
        }

        [Test]
        public void WeaponClick_WritesOnlyItsOwnSlot()
        {
            var loadout = ShowScreen(out _);

            RowButtons("PrimaryWeaponRow")[1].onClick.Invoke();
            Assert.AreSame(catalog.weapons[1], loadout.PrimaryWeapon, "primary click sets the primary slot");
            Assert.IsNull(loadout.SecondaryWeapon, "primary click leaves the secondary slot alone");

            RowButtons("SecondaryWeaponRow")[2].onClick.Invoke();
            Assert.AreSame(catalog.weapons[2], loadout.SecondaryWeapon, "secondary click sets the secondary slot");
            Assert.AreSame(catalog.weapons[1], loadout.PrimaryWeapon, "secondary click leaves the primary slot alone");
        }

        [Test]
        public void ShipPick_ReseedsWeaponsToAuthoredKit()
        {
            var loadout = ShowScreen(out var ship1);
            var authored = ship1.GetComponent<WeaponsController>();
            Assert.IsNotNull(authored.PrimaryMountPrefab, "test premise: Ship_1 has an authored primary");

            var offKit = System.Array.Find(catalog.weapons, w => w != authored.PrimaryMountPrefab);
            loadout.PrimaryWeapon = offKit;
            loadout.SecondaryWeapon = offKit;

            RowButtons("ShipRow")[0].onClick.Invoke();

            Assert.AreSame(authored.PrimaryMountPrefab, loadout.PrimaryWeapon,
                "ship pick reseeds the primary mount to the ship's authored kit");
            Assert.AreSame(authored.SecondaryMountPrefab, loadout.SecondaryWeapon,
                "ship pick reseeds the secondary mount to the ship's authored kit");
        }

        [Test]
        public void HangarStats_ReadableOnEveryCatalogWeaponAsset()
        {
            foreach (var path in WeaponPaths)
            {
                var weapon = Load<WeaponComponent>(path);
                Assert.IsNotEmpty(weapon.HangarStats, $"{path} produces a hangar stats line on the prefab asset");
            }
        }
    }
}
#endif

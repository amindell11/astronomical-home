#if UNITY_EDITOR
using System.Text.RegularExpressions;
using Combat.Weapons;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using Ships.Weapons;
using Tests.PlayMode.Common;
using UI.Hangar;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tests.PlayMode
{
    /// <summary>
    /// Headless-safe: the preview RawImage is removed before Show so no render-texture stage is
    /// ever created.
    /// </summary>
    [Category("UI")]
    public class HangarOptionRowsPlayModeTests : PlayModeWorldFixture
    {
        private const string ScreenPrefabPath = "Assets/Prefabs/UI/HangarScreen.prefab";
        private const string VanguardPath = "Assets/Prefabs/Ships/Vanguard.prefab";
        private const string RealCatalogPath = "Assets/Settings/Ships/PlayerLoadout.asset";

        private static readonly string[] WeaponPaths =
        {
            "Assets/Prefabs/Weapons/Lasers.prefab",
            "Assets/Prefabs/Weapons/Missiles.prefab",
            "Assets/Prefabs/Weapons/Railgun.prefab",
            "Assets/Prefabs/Weapons/Rippers.prefab",
        };

        private HangarScreen screen;
        private ItemSubset catalog;

        public override void TearDown()
        {
            DestroyTestObject(screen);
            if (catalog) Object.DestroyImmediate(catalog);
            base.TearDown();
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"asset loads: {path}");
            return asset;
        }

        private ShipLoadout ShowScreen(out Ship vanguard)
        {
            vanguard = Load<Ship>(VanguardPath);

            catalog = ScriptableObject.CreateInstance<ItemSubset>();
            catalog.ships = new[] { vanguard };
            var weapons = new WeaponComponent[WeaponPaths.Length];
            for (var i = 0; i < WeaponPaths.Length; i++)
                weapons[i] = Load<WeaponComponent>(WeaponPaths[i]);
            catalog.weapons = weapons;

            var loadout = new ShipLoadout(vanguard, null, null);
            Show(catalog, loadout);
            return loadout;
        }

        private void Show(ItemSubset offer, ShipLoadout loadout)
        {
            screen = Object.Instantiate(Load<HangarScreen>(ScreenPrefabPath));
            Object.DestroyImmediate(screen.GetComponentInChildren<RawImage>());
            screen.Show(offer, loadout, onLaunch: null);
        }

        private static void Next(OptionCycler row) => row.nextButton.onClick.Invoke();
        private static void Previous(OptionCycler row) => row.previousButton.onClick.Invoke();

        [Test]
        public void WeaponRows_CycleThroughCatalogAndWrap_WithDisplayNames()
        {
            ShowScreen(out _);

            foreach (var row in new[] { screen.primaryWeaponRow, screen.secondaryWeaponRow })
            {
                for (var i = 0; i < catalog.weapons.Length; i++)
                {
                    Next(row);
                    Assert.AreEqual(catalog.weapons[i].DisplayName, row.label.text,
                        $"{row.name} step {i + 1} shows weapon {i} by its display name");
                }

                Next(row);
                Assert.AreEqual(catalog.weapons[0].DisplayName, row.label.text, $"{row.name} wraps past the last weapon");
                Previous(row);
                Assert.AreEqual(catalog.weapons[^1].DisplayName, row.label.text, $"{row.name} wraps back past the first");
            }
        }

        [Test]
        public void Previous_FromAnEmptyMount_EntersAtTheLastWeapon()
        {
            var loadout = ShowScreen(out _);

            Previous(screen.primaryWeaponRow);

            Assert.AreSame(catalog.weapons[^1], loadout.PrimaryWeapon);
        }

        [Test]
        public void WeaponStep_WritesOnlyItsOwnSlot()
        {
            var loadout = ShowScreen(out _);

            Next(screen.primaryWeaponRow);
            Assert.AreSame(catalog.weapons[0], loadout.PrimaryWeapon, "primary step sets the primary slot");
            Assert.IsNull(loadout.SecondaryWeapon, "primary step leaves the secondary slot alone");

            Next(screen.secondaryWeaponRow);
            Next(screen.secondaryWeaponRow);
            Assert.AreSame(catalog.weapons[1], loadout.SecondaryWeapon, "secondary steps set the secondary slot");
            Assert.AreSame(catalog.weapons[0], loadout.PrimaryWeapon, "secondary steps leave the primary slot alone");
        }

        [Test]
        public void ShipStep_ReseedsWeaponsToAuthoredKit()
        {
            var loadout = ShowScreen(out var vanguard);
            var authored = vanguard.GetComponent<WeaponsController>();
            Assert.IsNotNull(authored.PrimaryMountPrefab, "test premise: Vanguard has an authored primary");

            var offKit = System.Array.Find(catalog.weapons, w => w != authored.PrimaryMountPrefab);
            loadout.PrimaryWeapon = offKit;
            loadout.SecondaryWeapon = offKit;

            Next(screen.shipRow);

            Assert.AreSame(authored.PrimaryMountPrefab, loadout.PrimaryWeapon,
                "ship step reseeds the primary mount to the ship's authored kit");
            Assert.AreSame(authored.SecondaryMountPrefab, loadout.SecondaryWeapon,
                "ship step reseeds the secondary mount to the ship's authored kit");
        }

        [Test]
        public void HoveredRowStats_FollowTheCycledPick()
        {
            ShowScreen(out _);
            var stats = screen.transform.Find("Panel/StatsText").GetComponent<Text>();
            var trigger = screen.primaryWeaponRow.GetComponent<EventTrigger>();
            var pointer = new PointerEventData(null);

            Next(screen.primaryWeaponRow);
            trigger.OnPointerEnter(pointer);
            Assert.AreEqual(HangarScreen.Describe(catalog.weapons[0]), stats.text, "hover shows the current pick");

            Next(screen.primaryWeaponRow);
            Assert.AreEqual(HangarScreen.Describe(catalog.weapons[1]), stats.text, "a step while hovered updates the readout");

            trigger.OnPointerExit(pointer);
            Assert.AreEqual("", stats.text, "leaving the row clears the readout");
        }

        // Read off the prefab asset, where Awake never runs: passes only if every stat source is serialized.
        // Patterns, not values, so balance tuning never breaks it.
        [TestCase("Assets/Prefabs/Weapons/Lasers.prefab",
            @"Damage \d+", @"Rate [\d.]+/s", @"Speed \d+", @"Overheats after \d+ shots")]
        [TestCase("Assets/Prefabs/Weapons/Railgun.prefab",
            @"Damage \d+", @"Range \d+", @"Full charge [\d.]+s", "Hitscan")]
        [TestCase("Assets/Prefabs/Weapons/Rippers.prefab",
            @"Damage \d+", @"Rate [\d.]+/s", @"Mag \d+ \(reload [\d.]+s\)", @"Speed \d+")]
        [TestCase("Assets/Prefabs/Weapons/Missiles.prefab",
            @"Damage \d+ \+ \d+ splash", @"\d+ rounds \(shared recharge [\d.]+s/round\)", "Lock-on homing",
            "Direct hit skips shields")]
        [TestCase("Assets/Prefabs/Weapons/Grenades.prefab",
            @"Blast \d+ to \d+u, hits friend and foe", @"\d+ charges \(shared recharge [\d.]+s/round\)", @"Range \d+u")]
        public void Describe_ShowsEveryStatOfCatalogWeaponAsset(string path, params string[] segmentPatterns)
        {
            var description = HangarScreen.Describe(Load<WeaponComponent>(path));
            var segments = description.Split(new[] { "   |   " }, System.StringSplitOptions.None);
            foreach (var pattern in segmentPatterns)
                Assert.IsTrue(System.Array.Exists(segments, s => Regex.IsMatch(s, $"^{pattern}$")),
                    $"shows a segment matching `{pattern}` in: {description}");
        }

        [Test]
        public void RealCatalog_EveryShipIsReachableByCycling()
        {
            var offer = Load<ItemSubset>(RealCatalogPath);
            var first = offer.ships[0];
            Show(offer, new ShipLoadout(first, null, null));
            Assert.AreEqual(first.name, screen.shipRow.label.text, "the ship row opens on the loadout's ship");

            for (var i = 1; i <= offer.ships.Length; i++)
            {
                Next(screen.shipRow);
                Assert.AreEqual(offer.ships[i % offer.ships.Length].name, screen.shipRow.label.text,
                    $"ship step {i} reaches offered ship {i % offer.ships.Length}");
            }
        }

        [Test]
        public void RealCatalog_ShipHover_NamesTheChassisEngineAndShield()
        {
            var offer = Load<ItemSubset>(RealCatalogPath);
            var first = offer.ships[0];
            var loadout = new ShipLoadout(first, null, null);
            Show(offer, loadout);

            var stats = screen.transform.Find("Panel/StatsText").GetComponent<Text>();
            screen.shipRow.GetComponent<EventTrigger>().OnPointerEnter(new PointerEventData(null));
            for (var i = 0; i < offer.ships.Length; i++)
            {
                Next(screen.shipRow);
                var lines = stats.text.Split('\n');
                var engine = $"Engine: {loadout.Ship.Engine.DisplayName}";
                var shield = $"Shield: {loadout.Ship.Shield.DisplayName}";
                StringAssert.StartsWith(engine, lines[1], $"ship step {i + 1} hover names the chassis's engine under the ship");
                StringAssert.StartsWith(shield, lines[2], $"ship step {i + 1} hover names the chassis's shield read-only under the engine");
                StringAssert.StartsWith($"Keystone: {loadout.Ship.KeystonePrefab.DisplayName}", lines[3], $"ship step {i + 1} hover names the chassis's keystone");
                StringAssert.DoesNotContain("_", engine + shield, $"ship step {i + 1} hover shows authored names");
            }
        }
    }
}
#endif

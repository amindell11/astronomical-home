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
        private const string ValisPath = "Assets/Prefabs/Ships/Valis.prefab";
        private const string CrimsonPath = "Assets/Prefabs/Ships/Crimson.prefab";
        private const string RealCatalogPath = "Assets/Settings/Ships/PlayerLoadout.asset";

        private static readonly string[] WeaponPaths =
        {
            "Assets/Prefabs/Weapons/Lasers.prefab",
            "Assets/Prefabs/Weapons/Missiles.prefab",
            "Assets/Prefabs/Weapons/Railgun.prefab",
            "Assets/Prefabs/Weapons/Rippers.prefab",
            "Assets/Prefabs/Weapons/Grenades.prefab",
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
            catalog.starter = vanguard;
            catalog.ships = new[] { vanguard };
            var weapons = new WeaponComponent[WeaponPaths.Length];
            for (var i = 0; i < WeaponPaths.Length; i++)
                weapons[i] = Load<WeaponComponent>(WeaponPaths[i]);
            catalog.weapons = weapons;

            var loadout = new ShipLoadout(vanguard, null, null);
            Show(catalog, loadout);
            return loadout;
        }

        private void Show(ItemSubset offer, ShipLoadout loadout, System.Func<Ship, bool> earned = null)
        {
            screen = Object.Instantiate(Load<HangarScreen>(ScreenPrefabPath));
            Object.DestroyImmediate(screen.GetComponentInChildren<RawImage>());
            screen.Show(offer, loadout, earned ?? (_ => true), onLaunch: null);
        }

        private ShipLoadout ShowLockedOffer(System.Func<Ship, bool> earned, out Ship vanguard, out Ship valis)
        {
            vanguard = Load<Ship>(VanguardPath);
            valis = Load<Ship>(ValisPath);
            catalog = ScriptableObject.CreateInstance<ItemSubset>();
            catalog.starter = vanguard;
            catalog.ships = new[] { vanguard, valis };
            catalog.weapons = System.Array.Empty<WeaponComponent>();

            var loadout = new ShipLoadout(Load<Ship>(CrimsonPath), null, null);
            Show(catalog, loadout, earned);
            return loadout;
        }

        private static void Next(OptionCycler row) => row.nextButton.onClick.Invoke();
        private static void Previous(OptionCycler row) => row.previousButton.onClick.Invoke();

        private static WeaponComponent[] OfKind(WeaponComponent[] pool, bool ordnance) =>
            System.Array.FindAll(pool, weapon => weapon.IsOrdnance == ordnance);

        [Test]
        public void WeaponRows_EachCycleTheirKindAndWrap_WithDisplayNames()
        {
            ShowScreen(out _);

            foreach (var (row, kind) in new[]
                     {
                         (screen.primaryWeaponRow, OfKind(catalog.weapons, ordnance: false)),
                         (screen.secondaryWeaponRow, OfKind(catalog.weapons, ordnance: true)),
                     })
            {
                Assert.That(kind.Length, Is.GreaterThan(1), "test premise: the row has a cycle to wrap");
                for (var i = 0; i < kind.Length; i++)
                {
                    Next(row);
                    Assert.AreEqual(kind[i].DisplayName, row.label.text,
                        $"{row.name} step {i + 1} shows its kind's item {i} by its display name");
                }

                Next(row);
                Assert.AreEqual(kind[0].DisplayName, row.label.text, $"{row.name} wraps past its kind's last item");
                Previous(row);
                Assert.AreEqual(kind[^1].DisplayName, row.label.text, $"{row.name} wraps back past its kind's first");
            }
        }

        [Test]
        public void Previous_FromAnEmptyMount_EntersAtTheLastWeapon()
        {
            var loadout = ShowScreen(out _);

            Previous(screen.primaryWeaponRow);

            Assert.AreSame(OfKind(catalog.weapons, ordnance: false)[^1], loadout.PrimaryWeapon);
        }

        [Test]
        public void WeaponStep_WritesOnlyItsOwnSlot()
        {
            var loadout = ShowScreen(out _);
            var weapons = OfKind(catalog.weapons, ordnance: false);
            var ordnance = OfKind(catalog.weapons, ordnance: true);

            Next(screen.primaryWeaponRow);
            Assert.AreSame(weapons[0], loadout.PrimaryWeapon, "the weapon step sets the weapon slot");
            Assert.IsNull(loadout.SecondaryWeapon, "the weapon step leaves the ordnance slot alone");

            Next(screen.secondaryWeaponRow);
            Next(screen.secondaryWeaponRow);
            Assert.AreSame(ordnance[1], loadout.SecondaryWeapon, "ordnance steps set the ordnance slot");
            Assert.AreSame(weapons[0], loadout.PrimaryWeapon, "ordnance steps leave the weapon slot alone");
        }

        [Test]
        public void RealCatalog_TheWeaponRowOffersOnlyWeapons_AndTheOrdnanceRowOnlyOrdnance()
        {
            var offer = Load<ItemSubset>(RealCatalogPath);
            var loadout = new ShipLoadout(offer.ships[0], null, null);
            Show(offer, loadout);
            Assert.AreEqual("WEAPON", screen.transform.Find("Panel/WEAPONLabel").GetComponent<Text>().text);
            Assert.AreEqual("ORDNANCE", screen.transform.Find("Panel/ORDNANCELabel").GetComponent<Text>().text);

            var weapons = OfKind(offer.weapons, ordnance: false);
            var ordnance = OfKind(offer.weapons, ordnance: true);
            Assert.IsNotEmpty(weapons, "test premise: the offer holds weapons");
            Assert.IsNotEmpty(ordnance, "test premise: the offer holds ordnance");
            CollectionAssert.AreEqual(weapons, Cycle(screen.primaryWeaponRow, weapons.Length, () => loadout.PrimaryWeapon));
            CollectionAssert.AreEqual(ordnance, Cycle(screen.secondaryWeaponRow, ordnance.Length, () => loadout.SecondaryWeapon));
        }

        private static WeaponComponent[] Cycle(OptionCycler row, int steps, System.Func<WeaponComponent> current)
        {
            var seen = new WeaponComponent[steps + 1];
            for (var i = 0; i <= steps; i++)
            {
                Next(row);
                seen[i] = current();
            }
            Assert.AreSame(seen[0], seen[steps], $"{row.name} wraps after its {steps} items");
            return seen[..steps];
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
        public void Show_PendingShipNotSelectable_ReseedsToTheStarterWithItsKit()
        {
            var loadout = ShowLockedOffer(_ => false, out var vanguard, out _);
            var authored = vanguard.GetComponent<WeaponsController>();

            Assert.AreSame(vanguard, loadout.Ship, "a pending ship the player can't fly reseeds to the starter");
            Assert.AreSame(authored.PrimaryMountPrefab, loadout.PrimaryWeapon, "the starter's authored primary");
            Assert.AreSame(authored.SecondaryMountPrefab, loadout.SecondaryWeapon, "the starter's authored secondary");
            Assert.AreEqual(vanguard.name, screen.shipRow.label.text);
            Assert.IsTrue(screen.launchButton.interactable, "the starter can launch");
        }

        [Test]
        public void LockedShip_ShowsLockedAndHoldsLaunch_UntilSteppedAway()
        {
            ShowLockedOffer(_ => false, out var vanguard, out var valis);
            var shown = screen.shipRow.label.color;

            Next(screen.shipRow);
            Assert.AreEqual($"{valis.name} (locked)", screen.shipRow.label.text);
            Assert.IsFalse(screen.launchButton.interactable, "a locked ship can't launch");
            Assert.Less(screen.shipRow.label.color.a, shown.a, "a locked ship's row is dimmed");

            Previous(screen.shipRow);
            Assert.AreEqual(vanguard.name, screen.shipRow.label.text);
            Assert.IsTrue(screen.launchButton.interactable, "stepping back to the starter re-enables launch");
            Assert.AreEqual(shown, screen.shipRow.label.color, "the starter's row is not dimmed");
        }

        [Test]
        public void EarnedShip_IsSelectable()
        {
            var valis = Load<Ship>(ValisPath);
            ShowLockedOffer(ship => ship == valis, out _, out _);

            Next(screen.shipRow);

            Assert.AreEqual(valis.name, screen.shipRow.label.text);
            Assert.IsTrue(screen.launchButton.interactable, "an earned ship can launch");
        }

        [Test]
        public void HoveredRowStats_FollowTheCycledPick()
        {
            ShowScreen(out _);
            var stats = screen.transform.Find("Panel/StatsText").GetComponent<Text>();
            var trigger = screen.primaryWeaponRow.GetComponent<EventTrigger>();
            var pointer = new PointerEventData(null);

            var weapons = OfKind(catalog.weapons, ordnance: false);

            Next(screen.primaryWeaponRow);
            trigger.OnPointerEnter(pointer);
            Assert.AreEqual(HangarScreen.Describe(weapons[0]), stats.text, "hover shows the current pick");

            Next(screen.primaryWeaponRow);
            Assert.AreEqual(HangarScreen.Describe(weapons[1]), stats.text, "a step while hovered updates the readout");

            trigger.OnPointerExit(pointer);
            Assert.AreEqual("", stats.text, "leaving the row clears the readout");
        }

        // Read off the prefab asset, where Awake never runs: passes only if every stat source is serialized.
        // Patterns, not values, so balance tuning never breaks it.
        [TestCase("Assets/Prefabs/Weapons/Lasers.prefab",
            @"Damage \d+", @"Rate [\d.]+/s", @"Speed \d+", @"Overheats after \d+ shots")]
        [TestCase("Assets/Prefabs/Weapons/TurboLasers.prefab",
            @"Damage \d+ per bolt", "2 bolts per shot", @"Rate [\d.]+/s", @"Speed \d+", @"Overheats after \d+ shots")]
        [TestCase("Assets/Prefabs/Weapons/Railgun.prefab",
            @"Damage \d+", @"Range \d+", @"Full charge [\d.]+s", "Hitscan")]
        [TestCase("Assets/Prefabs/Weapons/Rippers.prefab",
            @"Damage \d+ per slug", @"Rate [\d.]+/s", @"Mag \d+ \(reload [\d.]+s\)", @"Speed \d+")]
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

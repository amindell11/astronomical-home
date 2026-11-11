using System.Linq;
using System.Text.RegularExpressions;
using Combat.Weapons;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using Ships.Weapons;
using UI.Hangar.Displays;
using UI.Hangar.Selection;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Hangar
{
    [Category("UI")]
    public sealed class HangarSelectionEditModeTests
    {
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

        private ItemSubset offer;

        [TearDown]
        public void TearDown()
        {
            if (offer && !AssetDatabase.Contains(offer)) Object.DestroyImmediate(offer);
        }

        [Test]
        public void OfferWithShipsButNoStarter_Throws()
        {
            offer = ScriptableObject.CreateInstance<ItemSubset>();
            offer.name = "BareOffer";
            offer.ships = new[] { Load<Ship>(VanguardPath) };
            var loadout = new ShipLoadout(offer.ships[0], null, null);

            var exception = Assert.Throws<System.InvalidOperationException>(
                () => new HangarSelection(offer, loadout, _ => true));

            Assert.AreEqual("Hangar offer 'BareOffer' offers ships but names no starter.", exception.Message);
        }

        [Test]
        public void PendingShipNotSelectable_ReseedsToTheStarterWithItsKit()
        {
            var selection = LockedOffer(_ => false, out var vanguard, out _, out var loadout);
            var authored = vanguard.GetComponent<WeaponsController>();

            Assert.AreSame(vanguard, selection.Ship, "a pending ship the player can't fly reseeds to the starter");
            Assert.AreSame(authored.PrimaryMountPrefab, selection.Weapon, "the starter's authored primary");
            Assert.AreSame(authored.SecondaryMountPrefab, selection.Ordnance, "the starter's authored secondary");
            Assert.AreEqual(vanguard.name, selection.Label(HangarSlot.Ship));
            Assert.IsFalse(selection.ShipLocked);
            Assert.IsTrue(selection.CanLaunch, "the starter can launch");
            Assert.AreSame(vanguard, loadout.Ship);
        }

        [Test]
        public void LockedShip_StepsInAndBlocksLaunch_UntilSteppedAway()
        {
            var selection = LockedOffer(_ => false, out var vanguard, out var valis, out _);

            selection.Step(1);
            Assert.AreSame(valis, selection.Ship);
            Assert.AreEqual($"{valis.name} (locked)", selection.Label(HangarSlot.Ship));
            Assert.IsTrue(selection.ShipLocked);
            Assert.IsFalse(selection.CanLaunch, "a locked ship can't launch");

            selection.Step(-1);
            Assert.AreSame(vanguard, selection.Ship);
            Assert.AreEqual(vanguard.name, selection.Label(HangarSlot.Ship));
            Assert.IsFalse(selection.ShipLocked);
            Assert.IsTrue(selection.CanLaunch, "stepping back to the starter allows launch");
        }

        [Test]
        public void EarnedShip_CanLaunch()
        {
            var valis = Load<Ship>(ValisPath);
            var selection = LockedOffer(ship => ship == valis, out _, out _, out _);

            selection.Step(1);

            Assert.AreSame(valis, selection.Ship);
            Assert.AreEqual(valis.name, selection.Label(HangarSlot.Ship));
            Assert.IsFalse(selection.ShipLocked);
            Assert.IsTrue(selection.CanLaunch, "an earned ship can launch");
        }

        [Test]
        public void ShipStep_ReseedsWeaponsToAuthoredKit()
        {
            var selection = WeaponOffer(out var vanguard, out var loadout);
            var authored = vanguard.GetComponent<WeaponsController>();
            Assert.IsTrue(authored.PrimaryMountPrefab, "test premise: Vanguard has an authored primary");
            var offKit = System.Array.Find(offer.weapons, weapon => weapon != authored.PrimaryMountPrefab);
            loadout.PrimaryWeapon = offKit;
            loadout.SecondaryWeapon = offKit;

            selection.Step(1);

            Assert.AreSame(authored.PrimaryMountPrefab, selection.Weapon,
                "ship step reseeds the primary mount to the ship's authored kit");
            Assert.AreSame(authored.SecondaryMountPrefab, selection.Ordnance,
                "ship step reseeds the secondary mount to the ship's authored kit");
        }

        [Test]
        public void WeaponRows_EachCycleTheirKindAndWrap()
        {
            var selection = WeaponOffer(out _, out _);
            foreach (var (slot, kind) in new[]
                     {
                         (HangarSlot.Weapon, OfKind(offer.weapons, ordnance: false)),
                         (HangarSlot.Ordnance, OfKind(offer.weapons, ordnance: true))
                     })
            {
                Assert.That(kind.Length, Is.GreaterThan(1), "test premise: the slot has a cycle to wrap");
                selection.Select(slot);
                for (var i = 0; i < kind.Length; i++)
                {
                    selection.Step(1);
                    Assert.AreEqual(kind[i].DisplayName, selection.Label(slot),
                        $"{slot} step {i + 1} shows its kind's item {i} by its display name");
                }

                selection.Step(1);
                Assert.AreEqual(kind[0].DisplayName, selection.Label(slot), $"{slot} wraps past its kind's last item");
                selection.Step(-1);
                Assert.AreEqual(kind[^1].DisplayName, selection.Label(slot), $"{slot} wraps back past its kind's first");
            }
        }

        [Test]
        public void Previous_FromAnEmptyMount_EntersAtTheLastWeapon_AndNextAtTheFirst()
        {
            var selection = WeaponOffer(out _, out var loadout);
            var weapons = OfKind(offer.weapons, ordnance: false);
            selection.Select(HangarSlot.Weapon);

            selection.Step(-1);
            Assert.AreSame(weapons[^1], loadout.PrimaryWeapon);

            loadout.PrimaryWeapon = null;
            selection.Step(1);
            Assert.AreSame(weapons[0], loadout.PrimaryWeapon);
        }

        [Test]
        public void WeaponStep_WritesOnlyItsOwnSlot()
        {
            var selection = WeaponOffer(out _, out var loadout);
            var weapons = OfKind(offer.weapons, ordnance: false);
            var ordnance = OfKind(offer.weapons, ordnance: true);

            selection.Select(HangarSlot.Weapon);
            selection.Step(1);
            Assert.AreSame(weapons[0], loadout.PrimaryWeapon, "the weapon step sets the weapon slot");
            Assert.IsNull(loadout.SecondaryWeapon, "the weapon step leaves the ordnance slot alone");

            selection.Select(HangarSlot.Ordnance);
            selection.Step(1);
            selection.Step(1);
            Assert.AreSame(ordnance[1], loadout.SecondaryWeapon, "ordnance steps set the ordnance slot");
            Assert.AreSame(weapons[0], loadout.PrimaryWeapon, "ordnance steps leave the weapon slot alone");
        }

        [Test]
        public void RealCatalog_FiltersEachWeaponSlot()
        {
            offer = Load<ItemSubset>(RealCatalogPath);
            var loadout = new ShipLoadout(offer.ships[0], null, null);
            var selection = new HangarSelection(offer, loadout, _ => true);
            var weapons = OfKind(offer.weapons, ordnance: false);
            var ordnance = OfKind(offer.weapons, ordnance: true);
            Assert.IsNotEmpty(weapons, "test premise: the offer holds weapons");
            Assert.IsNotEmpty(ordnance, "test premise: the offer holds ordnance");
            CollectionAssert.AreEqual(weapons, Cycle(selection, HangarSlot.Weapon, weapons.Length, () => loadout.PrimaryWeapon));
            CollectionAssert.AreEqual(ordnance, Cycle(selection, HangarSlot.Ordnance, ordnance.Length, () => loadout.SecondaryWeapon));
        }

        [Test]
        public void NullWeaponPool_StepDoesNothing()
        {
            var ship = Load<Ship>(VanguardPath);
            offer = ScriptableObject.CreateInstance<ItemSubset>();
            offer.starter = ship;
            offer.ships = new[] { ship };
            var loadout = new ShipLoadout(ship, null, null);
            var selection = new HangarSelection(offer, loadout, _ => true);
            selection.Select(HangarSlot.Weapon);

            selection.Step(1);
            selection.Step(-1);

            Assert.IsNull(selection.Weapon);
            Assert.IsNull(selection.Ordnance);
        }

        [Test]
        public void RealCatalog_EveryShipIsReachableByCycling()
        {
            offer = Load<ItemSubset>(RealCatalogPath);
            var first = offer.ships[0];
            var selection = new HangarSelection(offer, new ShipLoadout(first, null, null), _ => true);
            Assert.AreEqual(first.name, selection.Label(HangarSlot.Ship), "the ship slot opens on the loadout's ship");

            for (var i = 1; i <= offer.ships.Length; i++)
            {
                selection.Step(1);
                Assert.AreEqual(offer.ships[i % offer.ships.Length].name, selection.Label(HangarSlot.Ship),
                    $"ship step {i} reaches offered ship {i % offer.ships.Length}");
            }
        }

        [Test]
        public void Turn_WrapsTheSlots_AndSelectOfTheCurrentSlotIsQuiet()
        {
            var selection = WeaponOffer(out _, out _);
            var changes = 0;
            selection.Changed += () => changes++;
            Assert.AreEqual(HangarSlot.Ship, selection.Selected);

            selection.Turn(-1);
            Assert.AreEqual(HangarSlot.Ordnance, selection.Selected);
            selection.Turn(1);
            Assert.AreEqual(HangarSlot.Ship, selection.Selected);
            selection.Turn(1);
            Assert.AreEqual(HangarSlot.Weapon, selection.Selected);
            selection.Turn(1);
            Assert.AreEqual(HangarSlot.Ordnance, selection.Selected);
            selection.Turn(1);
            Assert.AreEqual(HangarSlot.Ship, selection.Selected);
            selection.Select(HangarSlot.Ship);
            Assert.AreEqual(5, changes, "five turns are five changes; selecting the current slot again is not one");
        }

        [Test]
        public void ShipStep_RaisesTheDirection_AndATurnDoesNot()
        {
            var selection = LockedOffer(_ => false, out var vanguard, out var valis, out _);
            Ship reported = null;
            var reportedStep = 0;
            var shipEvents = 0;
            selection.ShipChanged += (ship, step) =>
            {
                reported = ship;
                reportedStep = step;
                shipEvents++;
            };

            selection.Turn(1);
            Assert.AreEqual(0, shipEvents, "moving between slots does not change the ship");

            selection.Select(HangarSlot.Ship);
            selection.Step(1);
            Assert.AreSame(valis, reported);
            Assert.AreEqual(1, reportedStep);
            selection.Step(-1);
            Assert.AreSame(vanguard, reported);
            Assert.AreEqual(-1, reportedStep);
            Assert.AreEqual(2, shipEvents);
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
            var description = HangarDescriptions.Describe(Load<WeaponComponent>(path));
            var segments = description.Split(new[] { "   |   " }, System.StringSplitOptions.None);
            foreach (var pattern in segmentPatterns)
                Assert.IsTrue(System.Array.Exists(segments, segment => Regex.IsMatch(segment, $"^{pattern}$")),
                    $"shows a segment matching `{pattern}` in: {description}");
        }

        [Test]
        public void Describe_NamesTheChassisEngineShieldAndKeystone()
        {
            offer = Load<ItemSubset>(RealCatalogPath);
            for (var i = 0; i < offer.ships.Length; i++)
            {
                var ship = offer.ships[i];
                var lines = HangarDescriptions.Describe(ship).Split('\n');
                var engine = $"Engine: {ship.Engine.DisplayName}";
                var shield = $"Shield: {ship.Shield.DisplayName}";
                StringAssert.StartsWith(engine, lines[1], $"ship {i + 1} names the chassis's engine");
                StringAssert.StartsWith(shield, lines[2], $"ship {i + 1} names the chassis's shield");
                StringAssert.StartsWith($"Keystone: {ship.KeystonePrefab.DisplayName}", lines[3],
                    $"ship {i + 1} names the chassis's keystone");
                StringAssert.DoesNotContain("_", engine + shield, $"ship {i + 1} shows authored names");
            }
        }

        private HangarSelection WeaponOffer(out Ship vanguard, out ShipLoadout loadout)
        {
            vanguard = Load<Ship>(VanguardPath);
            offer = ScriptableObject.CreateInstance<ItemSubset>();
            offer.starter = vanguard;
            offer.ships = new[] { vanguard };
            offer.weapons = WeaponPaths.Select(Load<WeaponComponent>).ToArray();
            loadout = new ShipLoadout(vanguard, null, null);
            return new HangarSelection(offer, loadout, _ => true);
        }

        private HangarSelection LockedOffer(System.Func<Ship, bool> earned, out Ship vanguard, out Ship valis,
            out ShipLoadout loadout)
        {
            vanguard = Load<Ship>(VanguardPath);
            valis = Load<Ship>(ValisPath);
            offer = ScriptableObject.CreateInstance<ItemSubset>();
            offer.starter = vanguard;
            offer.ships = new[] { vanguard, valis };
            offer.weapons = System.Array.Empty<WeaponComponent>();
            loadout = new ShipLoadout(Load<Ship>(CrimsonPath), null, null);
            return new HangarSelection(offer, loadout, earned);
        }

        private static WeaponComponent[] Cycle(HangarSelection selection, HangarSlot slot, int steps,
            System.Func<WeaponComponent> current)
        {
            selection.Select(slot);
            var seen = new WeaponComponent[steps + 1];
            for (var i = 0; i <= steps; i++)
            {
                selection.Step(1);
                seen[i] = current();
            }
            Assert.AreSame(seen[0], seen[steps], $"{slot} wraps after its {steps} items");
            return seen[..steps];
        }

        private static WeaponComponent[] OfKind(WeaponComponent[] pool, bool ordnance) =>
            System.Array.FindAll(pool, weapon => weapon.IsOrdnance == ordnance);

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"asset loads: {path}");
            return asset;
        }
    }
}

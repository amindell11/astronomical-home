using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Balance;
using Combat.Weapons.Conditions;
using NUnit.Framework;
using Ships;
using Ships.Command;
using Ships.Loadout;
using Ships.Weapons;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode
{
    /// <summary>The loadout stat hash and the stat fingerprint over the authored assets: a hull's prefab asset and its instance hash alike, only marked fields move a hash, and list order does not. Every edit lands on a scratch copy, never on an asset.</summary>
    [TestFixture]
    [Category("Ships")]
    public class StatHashEditModeTests
    {
        private const string HangarOfferPath = "Assets/Settings/Ships/PlayerLoadout.asset";
        private const string TrialSectorPath = "Assets/Prefabs/Sectors/TrialSector.prefab";
        private const float KillHullRestore = 0.25f;
        private const float StartInterval = 6f;

        private sealed class ScratchCommander : Commander
        {
            public override void Initialize(in ShipControl control) { }
        }

        private readonly List<Object> scratch = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var copy in scratch)
                if (copy) Object.DestroyImmediate(copy);
            scratch.Clear();
        }

        [Test]
        public void OfLoadout_PrefabAssetAndInstance_HashAlike_ForEveryHangarHull()
        {
            var hulls = HangarOffer().ships;
            foreach (var hull in hulls)
            {
                Assert.IsTrue(hull.Weapons, $"{hull.name}: the prefab asset has no weapons controller wired.");
                Assert.AreSame(hull.GetComponent<WeaponsController>(), hull.Weapons,
                    $"{hull.name}: the wired weapons controller must be the hull's own.");

                var assetLines = StatHash.Lines(hull);
                Assert.That(assetLines, Has.Some.StartsWith($"{hull.Weapons.PrimaryMountPrefab.name}/"),
                    $"{hull.name}: the prefab asset's lines must reach its primary mount's weapon.");

                var instance = Scratch(hull);
                CollectionAssert.AreEqual(assetLines, StatHash.Lines(instance), $"{hull.name}: asset and instance lines");
                Assert.AreEqual(StatHash.OfLoadout(hull), StatHash.OfLoadout(instance), $"{hull.name}: asset and instance hash");
            }

            Assert.AreEqual(hulls.Length, hulls.Select(StatHash.OfLoadout).Distinct().Count(),
                "Different hulls must not share a loadout stat hash.");
        }

        [Test]
        public void OfLoadout_EveryHangarWeapon_ReachesEachConditionOnItsPrefab()
        {
            var offer = HangarOffer();
            var ship = Scratch(offer.ships[0]);
            foreach (var weapon in offer.weapons)
            {
                ship.Weapons.primaryMount = weapon;
                var lines = StatHash.Lines(ship);
                foreach (var condition in weapon.GetComponents<WeaponCondition>())
                    Assert.That(lines, Has.Some.StartsWith($"{weapon.name}/{condition.GetType().Name}."),
                        $"{weapon.name}: {condition.GetType().Name} sits on the prefab but no marked reference reaches it.");
            }
        }

        [Test]
        public void Of_HashesOneItemsOwnSubtree_AndNothingElse()
        {
            var offer = HangarOffer();
            var ship = Scratch(offer.ships[0]);
            var weapon = Scratch(ship.Weapons.PrimaryMountPrefab);
            ship.Weapons.primaryMount = weapon;

            Assert.AreEqual(StatHash.Of(ship.Weapons.PrimaryMountPrefab), StatHash.Of(weapon),
                "A copy with the same name and numbers hashes like the prefab.");
            CollectionAssert.IsSubsetOf(StatHash.Lines(weapon), StatHash.Lines(ship),
                "A mounted weapon's lines are part of its ship's lines.");
            Assert.AreEqual(offer.weapons.Length, offer.weapons.Select(StatHash.Of).Distinct().Count(),
                "Different weapons must not share a hash.");

            var engine = StatHash.Of(ship.Engine);
            SetSerialized(weapon.GetComponent<Cooldown>(), "fireRate", property => property.floatValue += 0.01f);
            Assert.AreNotEqual(StatHash.Of(ship.Weapons.PrimaryMountPrefab), StatHash.Of(weapon),
                "A marked number on the weapon's condition moves the weapon's hash.");
            Assert.AreEqual(engine, StatHash.Of(ship.Engine), "A weapon edit leaves the engine's hash alone.");
        }

        [Test]
        public void OfLoadout_MovesWithMarkedFields_AndIgnoresUnmarkedOnes()
        {
            var ship = Scratch(HangarOffer().ships[0]);
            var weapon = Scratch(ship.Weapons.PrimaryMountPrefab);
            var baseline = StatHash.OfLoadout(ship);

            ship.Weapons.primaryMount = weapon;
            Assert.AreEqual(baseline, StatHash.OfLoadout(ship), "A copy with the same name and numbers is the same part.");

            ship.teamNumber += 1;
            SetSerialized(weapon, "displayName", property => property.stringValue += " (scratch)");
            Assert.AreEqual(baseline, StatHash.OfLoadout(ship), "Unmarked fields (team, display name) must not move the hash.");

            ship.maxHealth += 1f;
            var hullChanged = StatHash.OfLoadout(ship);
            Assert.AreNotEqual(baseline, hullChanged, "A marked hull number must move the hash.");

            SetSerialized(weapon.GetComponent<Cooldown>(), "fireRate", property => property.floatValue += 0.01f);
            Assert.AreNotEqual(hullChanged, StatHash.OfLoadout(ship),
                "A marked number on a condition, two references from the hull, must move the hash.");
        }

        [Test]
        public void OfSetting_IgnoresListOrder()
        {
            var offer = HangarOffer();
            var baseline = StatHash.OfSetting(offer, ScratchSector(RosterOf(offer.ships), StartInterval), KillHullRestore);

            var reordered = Object.Instantiate(offer);
            scratch.Add(reordered);
            Array.Reverse(reordered.ships);
            Array.Reverse(reordered.engines);
            Array.Reverse(reordered.shields);
            Array.Reverse(reordered.weapons);
            var reversedRoster = RosterOf(offer.ships.Reverse());

            Assert.AreEqual(baseline,
                StatHash.OfSetting(reordered, ScratchSector(reversedRoster, StartInterval), KillHullRestore));
        }

        [Test]
        public void OfSetting_MovesWhenTwoRosterEntriesSwapPilots()
        {
            var offer = HangarOffer();
            var first = ScratchPilot("FirstPilot");
            var second = ScratchPilot("SecondPilot");
            WaveDirector.RosterEntry Entry(int hull, Commander pilot) => new() { template = offer.ships[hull], pilot = pilot };

            var paired = ScratchSector(new[] { Entry(0, first), Entry(1, second) }, StartInterval);
            var swapped = ScratchSector(new[] { Entry(0, second), Entry(1, first) }, StartInterval);

            Assert.AreNotEqual(StatHash.OfSetting(offer, paired, KillHullRestore),
                StatHash.OfSetting(offer, swapped, KillHullRestore),
                "Which pilot flies which hull is part of what a run can draw from.");
        }

        [Test]
        public void OfSetting_MovesWithPacingAndKillRefill()
        {
            var offer = HangarOffer();
            var roster = RosterOf(offer.ships);
            var sector = ScratchSector(roster, StartInterval);
            var baseline = StatHash.OfSetting(offer, sector, KillHullRestore);

            Assert.AreNotEqual(baseline,
                StatHash.OfSetting(offer, ScratchSector(roster, StartInterval + 1f), KillHullRestore),
                "A pacing number on the wave director must move the fingerprint.");
            Assert.AreNotEqual(baseline, StatHash.OfSetting(offer, sector, KillHullRestore + 0.05f),
                "The kill refill must move the fingerprint.");
        }

        [Test]
        public void OfSetting_CoversTheTrialSectorsDirectorAndItsEnemyPool()
        {
            var sector = AssetDatabase.LoadAssetAtPath<Sector>(TrialSectorPath);
            Assert.IsNotNull(sector, $"Trial sector prefab missing at {TrialSectorPath}");
            var director = sector.Spawners.OfType<WaveDirector>().Single();

            var lines = StatHash.SettingLines(HangarOffer(), sector, KillHullRestore);

            Assert.That(lines, Has.Some.StartsWith($"{director.name}/WaveDirector.startInterval="));
            Assert.That(lines, Has.Some.StartsWith($"{director.name}/WaveDirector.roster={{"));
            Assert.That(lines, Has.Some.StartsWith("TrialEnemyLoadout/LoadoutConfig.weapons="));
            Assert.That(lines, Has.Some.StartsWith("PlayerLoadout/LoadoutConfig.ships="));
            Assert.That(lines, Has.Member("setting/killHullRestore=0.25"));
        }

        [Test]
        public void OfLoadout_DoesNotDependOnTheMachinesCulture()
        {
            var hull = HangarOffer().ships[0];
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                var expected = StatHash.OfLoadout(hull);

                // German writes 0.5 as "0,5".
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                Assert.AreEqual(expected, StatHash.OfLoadout(hull));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        private static LoadoutConfig HangarOffer()
        {
            var offer = AssetDatabase.LoadAssetAtPath<LoadoutConfig>(HangarOfferPath);
            Assert.IsNotNull(offer, $"Hangar offer missing at {HangarOfferPath}");
            return offer;
        }

        private static WaveDirector.RosterEntry[] RosterOf(IEnumerable<Ship> hulls) =>
            hulls.Select(hull => new WaveDirector.RosterEntry { template = hull }).ToArray();

        private T Scratch<T>(T original) where T : Component
        {
            var copy = Object.Instantiate(original);
            scratch.Add(copy.gameObject);
            return copy;
        }

        private Commander ScratchPilot(string pilotName)
        {
            var pilot = new GameObject(pilotName).AddComponent<ScratchCommander>();
            scratch.Add(pilot.gameObject);
            return pilot;
        }

        private Sector ScratchSector(WaveDirector.RosterEntry[] roster, float startInterval)
        {
            var sector = new GameObject("ScratchSector").AddComponent<Sector>();
            scratch.Add(sector.gameObject);
            var director = new GameObject("WaveDirector").AddComponent<WaveDirector>();
            director.transform.SetParent(sector.transform);
            director.Configure(roster, startInterval, endInterval: 2f, startCap: 2, endCap: 8, rampSeconds: 240f);
            sector.SetManifest(null, new SectorSpawner[] { director }, null);
            return sector;
        }

        private static void SetSerialized(Object target, string property, Action<SerializedProperty> edit)
        {
            var serialized = new SerializedObject(target);
            edit(serialized.FindProperty(property));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Damage;
using Game;
using Game.Companion;
using Game.Moments;
using Game.Runs;
using NUnit.Framework;
using Ships.Registry;
using Substrate.Sectors.Elements.Elites;
using Substrate.Sectors.Elements.Hunt;
using Substrate.Sectors.Elements.Stations;
using UnityEditor;

namespace Tests.EditMode.Companion
{
    [Category("UI")]
    public class CompanionMomentsEditModeTests
    {
        private const string SheetPath = "Assets/Settings/Game/Companion/CompanionLines.asset";
        private const string FeelPath = "Assets/Settings/Game/Companion/CompanionFeel.asset";
        private const string GameHostPath = "Assets/Prefabs/Game/GameHost.prefab";

        private string dir;
        private string path;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "companion-moments-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            path = Path.Combine(dir, "run-records.jsonl");
        }

        [TearDown]
        public void TearDown() => Directory.Delete(dir, true);

        private static string Record(string outcome, int sectorsCleared, int kills, string killedBy) => new RunRecord
        {
            schema = RunRecord.SchemaId,
            endedUtc = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc)
                .ToString(RunRecord.EndedUtcFormat, CultureInfo.InvariantCulture),
            outcome = outcome,
            sectorsCleared = sectorsCleared,
            kills = kills,
            killingBlow = new KillingBlowRow { kind = killedBy, spawn = RunRecord.NoSpawn },
        }.ToJsonLine();

        private RunStarted StartAfter(params string[] records)
        {
            File.WriteAllLines(path, records);
            return new RunStarted(PastRuns.Read(path));
        }

        [Test]
        public void FirstLaunch_SpeaksTheFirstLaunchSlot()
        {
            var moment = new RunStarted(PastRuns.Read(path));

            Assert.IsTrue(moment.FirstLaunch);
            Assert.AreEqual(RunStarted.FirstLaunchSlot, moment.Slot);
            Assert.AreEqual(1, moment.CloneNumber);
            Assert.AreEqual("1", moment.Facts["clone"]);
            Assert.AreEqual(string.Empty, moment.Facts["last_sector"]);
            Assert.AreEqual(string.Empty, moment.Facts["last_killed_by"]);
        }

        [Test]
        public void AfterABestDeath_TheBestWins()
        {
            var moment = StartAfter(Record(RunRecord.Died, 0, 1, "Laser"), Record(RunRecord.Died, 1, 1, "Missile"));

            Assert.AreEqual(RunStarted.AfterBestSlot, moment.Slot);
            Assert.AreEqual("3", moment.Facts["clone"]);
            Assert.AreEqual("2", moment.Facts["last_sector"]);
            Assert.AreEqual("Missile", moment.Facts["last_killed_by"]);
        }

        [Test]
        public void AfterADeath_SpeaksTheAfterDeathSlot()
        {
            var moment = StartAfter(Record(RunRecord.Died, 2, 1, "Laser"), Record(RunRecord.Died, 0, 1, "Railgun"));

            Assert.IsFalse(moment.FirstLaunch);
            Assert.AreEqual(RunStarted.AfterDeathSlot, moment.Slot);
            Assert.AreEqual(3, moment.CloneNumber);
            Assert.AreEqual("1", moment.Facts["last_sector"]);
            Assert.AreEqual("Railgun", moment.Facts["last_killed_by"]);
        }

        [Test]
        public void AfterAnExtraction_SpeaksTheAfterExtractionSlot()
        {
            var moment = StartAfter(Record(RunRecord.Extracted, 3, 9, string.Empty),
                Record(RunRecord.Extracted, 3, 2, string.Empty));

            Assert.AreEqual(RunStarted.AfterExtractionSlot, moment.Slot);
            Assert.AreEqual("3", moment.Facts["last_sector"]);
            Assert.AreEqual(string.Empty, moment.Facts["last_killed_by"]);
        }

        [Test]
        public void PlayerDied_QuotesItsSectorAndKiller()
        {
            var moment = new PlayerDied(2, DamageKind.Missile);

            Assert.AreEqual(PlayerDied.DeathSlot, moment.Slot);
            Assert.AreEqual("2", moment.Facts["sector"]);
            Assert.AreEqual("Missile", moment.Facts["killed_by"]);
        }

        [Test]
        public void StationArrived_MapsEachKindToItsSlotAndCarriesTheSector()
        {
            var repair = new StationArrived(StationKind.Repair, 2);
            Assert.AreEqual("station-arrived-repair", StationArrived.RepairSlot);
            Assert.AreEqual(StationArrived.RepairSlot, repair.Slot);
            Assert.AreEqual(2, repair.Sector);
            Assert.AreEqual("2", repair.Facts["sector"]);

            var mod = new StationArrived(StationKind.Mod, 1);
            Assert.AreEqual("station-arrived-mod", StationArrived.ModSlot);
            Assert.AreEqual(StationArrived.ModSlot, mod.Slot);
            Assert.AreEqual("1", mod.Facts["sector"]);

            var armory = new StationArrived(StationKind.Armory, 4);
            Assert.AreEqual("station-arrived-armory", StationArrived.ArmorySlot);
            Assert.AreEqual(StationArrived.ArmorySlot, armory.Slot);
            Assert.AreEqual("4", armory.Facts["sector"]);
        }

        [Test]
        public void StationArrived_UnmappedKind_ThrowsNamingTheKind()
        {
            var kind = (StationKind)99;
            var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => new StationArrived(kind, 1));

            Assert.AreEqual("kind", thrown.ParamName);
            StringAssert.Contains(kind.ToString(), thrown.Message);
        }

        [Test]
        public void PatrolSpotted_CarriesTheSectorFactionAndPatrol()
        {
            var moment = new PatrolSpotted(5, "veil", new ShipId(42));

            Assert.AreEqual("patrol-spotted", PatrolSpotted.SpottedSlot);
            Assert.AreEqual(PatrolSpotted.SpottedSlot, moment.Slot);
            Assert.AreEqual(5, moment.Sector);
            Assert.AreEqual("veil", moment.Faction);
            Assert.AreEqual(new ShipId(42), moment.Patrol);
            Assert.AreEqual("5", moment.Facts["sector"]);
            Assert.AreEqual("veil", moment.Facts["faction"]);
        }

        [Test]
        public void AlarmRaised_MapsEachReasonAndKeepsANullPatrol()
        {
            var patrol = new AlarmRaised(5, "veil", HuntAlarmReason.Patrol, new ShipId(42));
            Assert.AreEqual("alarm-raised", AlarmRaised.AlarmSlot);
            Assert.AreEqual(AlarmRaised.AlarmSlot, patrol.Slot);
            Assert.AreEqual(5, patrol.Sector);
            Assert.AreEqual("veil", patrol.Faction);
            Assert.AreEqual(HuntAlarmReason.Patrol, patrol.Reason);
            Assert.AreEqual(new ShipId(42), patrol.Patrol.Value);
            Assert.AreEqual("5", patrol.Facts["sector"]);
            Assert.AreEqual("veil", patrol.Facts["faction"]);
            Assert.AreEqual("patrol", patrol.Facts["reason"]);

            var clock = new AlarmRaised(1, "", HuntAlarmReason.SearchClock, null);
            Assert.AreEqual(HuntAlarmReason.SearchClock, clock.Reason);
            Assert.IsFalse(clock.Patrol.HasValue);
            Assert.AreEqual(string.Empty, clock.Facts["faction"]);
            Assert.AreEqual("search-clock", clock.Facts["reason"]);
        }

        [Test]
        public void AlarmRaised_UnmappedReason_ThrowsNamingTheReason()
        {
            var reason = (HuntAlarmReason)99;
            var thrown = Assert.Throws<ArgumentOutOfRangeException>(
                () => new AlarmRaised(1, "veil", reason, null));

            Assert.AreEqual("reason", thrown.ParamName);
            StringAssert.Contains(reason.ToString(), thrown.Message);
        }

        [Test]
        public void PatrolSilenced_CarriesTheSectorFactionAndPatrol()
        {
            var moment = new PatrolSilenced(5, "veil", new ShipId(42));

            Assert.AreEqual("patrol-silenced", PatrolSilenced.SilencedSlot);
            Assert.AreEqual(PatrolSilenced.SilencedSlot, moment.Slot);
            Assert.AreEqual(5, moment.Sector);
            Assert.AreEqual("veil", moment.Faction);
            Assert.AreEqual(new ShipId(42), moment.Patrol);
            Assert.AreEqual("5", moment.Facts["sector"]);
            Assert.AreEqual("veil", moment.Facts["faction"]);
        }

        [Test]
        public void EliteAnnounced_ConvertsTheSectorAndCarriesTheShip()
        {
            var moment = new EliteAnnounced(new EliteMoment(4, "veil", new ShipId(42)));

            Assert.AreEqual("elite-announced", EliteAnnounced.AnnouncedSlot);
            Assert.AreEqual(EliteAnnounced.AnnouncedSlot, moment.Slot);
            Assert.AreEqual(5, moment.Sector);
            Assert.AreEqual("veil", moment.Faction);
            Assert.AreEqual(new ShipId(42), moment.Ship);
            Assert.AreEqual("5", moment.Facts["sector"]);
            Assert.AreEqual("veil", moment.Facts["faction"]);
        }

        [Test]
        public void EliteBeaten_ConvertsTheSectorAndCarriesTheShip()
        {
            var moment = new EliteBeaten(new EliteMoment(4, "veil", new ShipId(42)));

            Assert.AreEqual("elite-beaten", EliteBeaten.BeatenSlot);
            Assert.AreEqual(EliteBeaten.BeatenSlot, moment.Slot);
            Assert.AreEqual(5, moment.Sector);
            Assert.AreEqual("veil", moment.Faction);
            Assert.AreEqual(new ShipId(42), moment.Ship);
            Assert.AreEqual("5", moment.Facts["sector"]);
            Assert.AreEqual("veil", moment.Facts["faction"]);
        }

        [Test]
        public void ShipEarned_ConvertsTheSectorAndNamesTheChassis()
        {
            var moment = new ShipEarned(new ShipUnlocked(4, "veil", "wasp", true));

            Assert.AreEqual("ship-unlocked", ShipEarned.EarnedSlot);
            Assert.AreEqual(ShipEarned.EarnedSlot, moment.Slot);
            Assert.AreEqual(5, moment.Sector);
            Assert.AreEqual("veil", moment.Faction);
            Assert.AreEqual("wasp", moment.Chassis);
            Assert.IsTrue(moment.FirstOfAny);
            Assert.AreEqual("5", moment.Facts["sector"]);
            Assert.AreEqual("veil", moment.Facts["faction"]);
            Assert.AreEqual("wasp", moment.Facts["ship"]);
        }

        [Test]
        public void SectorEntered_CarriesItsFaction()
        {
            var named = new SectorEntered(2, "veil");
            Assert.AreEqual("veil", named.Faction);
            Assert.AreEqual("veil", named.Facts["faction"]);
            Assert.AreEqual("2", named.Facts["sector"]);

            var empty = new SectorEntered(1, "");
            Assert.AreEqual(string.Empty, empty.Faction);
            Assert.AreEqual(string.Empty, empty.Facts["faction"]);
        }

        [Test]
        public void SectorMoments_RejectsANullStreamOrFaction_AndAllowsNeitherHuntNorElite()
        {
            var stream = new RunMomentStream();
            var missingStream = Assert.Throws<ArgumentNullException>(() => new SectorMoments(null, "", null, null));
            Assert.AreEqual("moments", missingStream.ParamName);
            var missingFaction = Assert.Throws<ArgumentNullException>(() => new SectorMoments(stream, null, null, null));
            Assert.AreEqual("faction", missingFaction.ParamName);

            var wired = new SectorMoments(stream, "", null, null);
            Assert.DoesNotThrow(wired.Dispose);
        }

        [Test]
        public void ShippedSheet_HasAMustSayLineSlotForEveryMoment()
        {
            var sheet = AssetDatabase.LoadAssetAtPath<CompanionLineSheet>(SheetPath);
            Assert.IsTrue(sheet, "the line sheet ships");

            foreach (var moment in RunStarted.AllSlots.Append(PlayerDied.DeathSlot))
                Assert.IsTrue(sheet.Find(moment).MustSay, $"{moment} is must-say");
        }

        [Test]
        public void ShippedSheet_HasALineSlotForEveryInRunMoment()
        {
            var sheet = AssetDatabase.LoadAssetAtPath<CompanionLineSheet>(SheetPath);

            foreach (var moment in new[]
                     {
                         SectorEntered.SectorSlot, CacheClaimed.CacheSlot, ModPicked.PickSlot, ArmoryPicked.PickSlot,
                         CloseCall.CloseCallSlot, StationArrived.RepairSlot, StationArrived.ModSlot,
                         StationArrived.ArmorySlot, PatrolSpotted.SpottedSlot, AlarmRaised.AlarmSlot,
                         PatrolSilenced.SilencedSlot, EliteAnnounced.AnnouncedSlot, EliteBeaten.BeatenSlot,
                         ShipEarned.EarnedSlot,
                     })
                Assert.DoesNotThrow(() => sheet.Find(moment), $"{moment} has a line slot");
        }

        [Test]
        public void ShippedSheet_HuntEliteAndUnlockSlotsAreNotMustSay()
        {
            var lines = AssetDatabase.LoadAssetAtPath<CompanionLineSheet>(SheetPath);

            foreach (var slot in new[]
                     {
                         PatrolSpotted.SpottedSlot, AlarmRaised.AlarmSlot, PatrolSilenced.SilencedSlot,
                         EliteAnnounced.AnnouncedSlot, EliteBeaten.BeatenSlot, ShipEarned.EarnedSlot,
                     })
                Assert.IsFalse(lines.Find(slot).MustSay, $"{slot} is not must-say");
        }

        [Test]
        public void ShippedGameHost_ReferencesTheShippedSheetAndFeel()
        {
            var host = AssetDatabase.LoadAssetAtPath<GameHost>(GameHostPath);

            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<CompanionLineSheet>(SheetPath), host.companionLines);
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<CompanionFeelProfile>(FeelPath), host.companionFeel);
            Assert.IsTrue(host.companionLines && host.companionFeel, "test premise: both assets load");
        }

    }
}

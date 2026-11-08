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
                         SectorEntered.SectorSlot, BeaconClaimed.BeaconSlot, ModPicked.PickSlot,
                         CloseCall.CloseCallSlot,
                     })
                Assert.DoesNotThrow(() => sheet.Find(moment), $"{moment} has a line slot");
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

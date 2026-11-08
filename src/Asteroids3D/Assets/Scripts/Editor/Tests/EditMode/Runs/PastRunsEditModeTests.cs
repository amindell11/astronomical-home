using System;
using System.Globalization;
using System.IO;
using Game.Runs;
using NUnit.Framework;

namespace Tests.EditMode.Runs
{
    [Category("Bootstrap")]
    public class PastRunsEditModeTests
    {
        private static readonly DateTime Ended = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

        private string dir;
        private string path;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "past-runs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            path = Path.Combine(dir, "run-records.jsonl");
        }

        [TearDown]
        public void TearDown() => Directory.Delete(dir, true);

        private static string Died(int sectorsCleared, int kills, string killedBy) =>
            Record(RunRecord.Died, sectorsCleared, kills, killedBy);

        private static string Extracted(int sectorsCleared, int kills) =>
            Record(RunRecord.Extracted, sectorsCleared, kills, string.Empty);

        private static string Record(string outcome, int sectorsCleared, int kills, string killedBy) => new RunRecord
        {
            schema = RunRecord.SchemaId,
            endedUtc = Ended.ToString(RunRecord.EndedUtcFormat, CultureInfo.InvariantCulture),
            outcome = outcome,
            sectorsCleared = sectorsCleared,
            kills = kills,
            secondsSurvived = 60f,
            killingBlow = new KillingBlowRow { kind = killedBy, spawn = RunRecord.NoSpawn },
        }.ToJsonLine();

        [Test]
        public void MissingStore_HasNoPastRuns()
        {
            var past = PastRuns.Read(path);

            Assert.AreEqual(0, past.Count);
            Assert.IsNull(past.Last);
        }

        [Test]
        public void LastDeath_NamesItsKillerAndTheSectorItHappenedIn()
        {
            File.WriteAllLines(path, new[] { Extracted(1, 3), Died(2, 9, "Missile"), Died(0, 1, "Laser") });

            var past = PastRuns.Read(path);

            Assert.AreEqual(3, past.Count);
            var last = past.Last.Value;
            Assert.IsTrue(last.Died);
            Assert.AreEqual("Laser", last.KillingBlowKind);
            Assert.AreEqual(1, last.Sector, "a death with no sector cleared happened in sector 1");
        }

        [Test]
        public void LastExtraction_EndedInItsLastClearedSector()
        {
            File.WriteAllLines(path, new[] { Died(0, 1, "Laser"), Died(1, 2, "Railgun"), Extracted(3, 4) });

            var last = PastRuns.Read(path).Last.Value;

            Assert.IsFalse(last.Died);
            Assert.AreEqual(string.Empty, last.KillingBlowKind);
            Assert.AreEqual(3, last.Sector);
        }

        [Test]
        public void WasBest_OnlyWhenTheLastRunOutranksEveryEarlierRun()
        {
            File.WriteAllLines(path, new[] { Died(1, 5, "Laser"), Died(2, 1, "Laser") });
            Assert.IsTrue(PastRuns.Read(path).Last.Value.WasBest, "more sectors cleared than any earlier run");

            File.WriteAllLines(path, new[] { Died(1, 5, "Laser"), Died(1, 5, "Laser") });
            Assert.IsFalse(PastRuns.Read(path).Last.Value.WasBest, "a tie keeps the earlier run above");

            File.WriteAllLines(path, new[] { Died(2, 5, "Laser"), Died(1, 9, "Laser") });
            Assert.IsFalse(PastRuns.Read(path).Last.Value.WasBest, "an earlier run ranks higher");
        }

        [Test]
        public void UnreadableLines_AreNotCounted()
        {
            File.WriteAllLines(path, new[]
            {
                "{\"schema\":\"run-record-v1\",\"endedUtc\":\"2026-10-03T03:25:21Z\",\"kills\":40}",
                Died(0, 1, "Missile"),
                "not json",
            });

            var past = PastRuns.Read(path);

            Assert.AreEqual(1, past.Count);
            Assert.AreEqual("Missile", past.Last.Value.KillingBlowKind);
        }
    }
}

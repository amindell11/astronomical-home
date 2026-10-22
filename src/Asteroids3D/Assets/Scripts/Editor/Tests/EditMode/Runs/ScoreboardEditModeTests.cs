using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Runs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode.Runs
{
    /// <summary>The scoreboard over a temp run-record file: only v2 lines reach it, it ranks by sectors cleared, then kills, then seconds survived, this run takes its place below the earlier runs it ties, and an unrecorded run or an unreadable file is not ranked.</summary>
    [Category("Bootstrap")]
    public class ScoreboardEditModeTests
    {
        private static readonly DateTime Ended = new(2026, 10, 20, 21, 30, 0, DateTimeKind.Utc);

        private string dir;
        private string path;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "scoreboard-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            path = Path.Combine(dir, "run-records.jsonl");
        }

        [TearDown]
        public void TearDown() => Directory.Delete(dir, true);

        private static Scoreboard.Row Row(int sectors, int kills, float seconds) => new(sectors, kills, seconds, Ended);

        private static RunRecord Record(int sectors, int kills, float seconds) => new()
        {
            schema = RunRecord.SchemaId,
            endedUtc = Ended.ToString(RunRecord.EndedUtcFormat, CultureInfo.InvariantCulture),
            sectorsCleared = sectors,
            kills = kills,
            secondsSurvived = seconds,
        };

        private static string Shape(Scoreboard.Row row) => $"{row.SectorsCleared}/{row.Kills}/{row.SecondsSurvived}";

        [Test]
        public void Read_KeepsOnlyReadableV2Lines()
        {
            File.WriteAllLines(path, new[]
            {
                "{\"schema\":\"run-record-v1\",\"endedUtc\":\"2026-10-03T03:25:21Z\",\"kills\":40,\"secondsSurvived\":900}",
                Record(1, 5, 60f).ToJsonLine(),
                "",
                "not json",
                Record(3, 9, 30f).ToJsonLine().Substring(0, 20),
                "{\"schema\":\"run-record-v2\",\"endedUtc\":\"yesterday\",\"sectorsCleared\":3}",
                Record(2, 7, 120f).ToJsonLine(),
            });

            var board = Scoreboard.Read(path, null);

            CollectionAssert.AreEqual(new[] { "2/7/120", "1/5/60" }, board.Rows.Select(Shape));
            Assert.AreEqual(Ended, board.Rows[0].EndedUtc);
            Assert.AreEqual(DateTimeKind.Utc, board.Rows[0].EndedUtc.Kind);
        }

        [Test]
        public void Rows_RankBySectorsThenKillsThenSeconds()
        {
            var board = new Scoreboard(new[] { Row(1, 9, 500f), Row(2, 3, 10f), Row(2, 3, 40f), Row(2, 8, 5f) }, null);

            CollectionAssert.AreEqual(new[] { "2/8/5", "2/3/40", "2/3/10", "1/9/500" }, board.Rows.Select(Shape));
        }

        [Test]
        public void ThisRun_TakesItsPlaceAmongEveryRecordedRun()
        {
            var board = new Scoreboard(new[] { Row(3, 1, 10f), Row(0, 9, 99f), Row(1, 2, 10f) }, Row(1, 5, 10f));

            Assert.AreEqual(2, board.Rank);
            Assert.AreEqual(4, board.Rows.Count);
            Assert.AreEqual("1/5/10", Shape(board.Rows[1]));
        }

        [Test]
        public void ThisRun_TyingAnEarlierRun_RanksBelowIt()
        {
            var earlier = new Scoreboard.Row(2, 4, 50f, Ended.AddDays(-1));

            var board = new Scoreboard(new[] { earlier }, Row(2, 4, 50f));

            Assert.AreEqual(2, board.Rank);
            Assert.AreEqual(earlier.EndedUtc, board.Rows[0].EndedUtc, "the earlier run keeps the place");
            Assert.AreEqual(Ended, board.Rows[1].EndedUtc);
        }

        [Test]
        public void Read_ThisRunWithNoFileYet_RanksFirstOfOne()
        {
            var board = Scoreboard.Read(path, Record(0, 3, 20f));

            Assert.AreEqual(1, board.Rank);
            Assert.AreEqual(1, board.Rows.Count);
        }

        [Test]
        public void Read_UnrecordedRun_IsNotRanked_AndTheBoardIsTheFile()
        {
            File.WriteAllLines(path, new[] { Record(1, 1, 1f).ToJsonLine() });

            var board = Scoreboard.Read(path, null);

            Assert.IsNull(board.Rank);
            Assert.AreEqual(1, board.Rows.Count);
        }

        [Test]
        public void Read_FileThatCannotBeRead_LogsThePath_AndRanksNothing()
        {
            File.WriteAllLines(path, new[] { Record(1, 1, 1f).ToJsonLine() });
            using var hold = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            LogAssert.Expect(LogType.Error, new Regex("Scoreboard not read from '" + Regex.Escape(path) + "'"));
            var board = Scoreboard.Read(path, Record(2, 2, 2f));

            Assert.IsNull(board.Rank);
            Assert.AreEqual(0, board.Rows.Count);
        }
    }
}

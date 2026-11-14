using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Game.Runs
{
    /// <summary>
    /// Every died or extracted v2 or v3 run in the run-record store, best first, and where this run places
    /// among them. Abandoned lines, v1 lines and lines that do not parse stay in the file and off the board.
    /// <see cref="Compare"/> is the whole ranking rule, so a scoring system replaces that one method.
    /// The rule and what the board shows:
    /// https://github.com/amindell11/astronomical-home/issues/700#issuecomment-6307786977
    /// </summary>
    public sealed class Scoreboard
    {
        public readonly struct Row
        {
            public readonly int SectorsCleared;
            public readonly int Kills;
            public readonly float SecondsSurvived;
            public readonly DateTime EndedUtc;
            public readonly LoadoutRow Player;

            public Row(int sectorsCleared, int kills, float secondsSurvived, DateTime endedUtc,
                LoadoutRow player = default)
            {
                SectorsCleared = sectorsCleared;
                Kills = kills;
                SecondsSurvived = secondsSurvived;
                EndedUtc = endedUtc;
                Player = player;
            }
        }

        /// <summary>Best first; a tie keeps the earlier run above.</summary>
        public IReadOnlyList<Row> Rows { get; }

        /// <summary>This run's place in <see cref="Rows"/>, counted from 1; null when it is not on the board.</summary>
        public int? Rank { get; }

        public Scoreboard(IReadOnlyList<Row> recorded, Row? thisRun)
        {
            var rows = new List<Row>(recorded);
            if (thisRun is { } run)
            {
                Rank = 1 + recorded.Count(row => Compare(row, run) <= 0);
                rows.Add(run);
            }
            Rows = rows.OrderBy(row => row, Comparer<Row>.Create(Compare)).ToList();
        }

        /// <summary>Negative when <paramref name="a"/> ranks above <paramref name="b"/>.</summary>
        public static int Compare(Row a, Row b)
        {
            var bySectors = b.SectorsCleared.CompareTo(a.SectorsCleared);
            if (bySectors != 0) return bySectors;
            var byKills = b.Kills.CompareTo(a.Kills);
            return byKills != 0 ? byKills : b.SecondsSurvived.CompareTo(a.SecondsSurvived);
        }

        /// <summary>Read before this run's record is appended, so the run enters the board once.</summary>
        public static Scoreboard Read(string path, RunRecord? thisRun)
        {
            Row? mine = null;
            if (thisRun is { } run)
                mine = TryRead(run, out var row)
                    ? row
                    : throw new ArgumentException("This run's record is not a readable run record.", nameof(thisRun));

            var runs = ReadRuns(path);
            return runs == null
                ? new Scoreboard(Array.Empty<Row>(), null)
                : new Scoreboard(runs.ConvertAll(run => run.Row), mine);
        }

        /// <summary>Every readable run in file order; null when the file cannot be read, which is logged.</summary>
        internal static List<(RunRecord Record, Row Row)> ReadRuns(string path)
        {
            var records = ReadRecords(path);
            if (records == null) return null;

            var runs = new List<(RunRecord, Row)>(records.Count);
            foreach (var record in records)
                if (TryRead(record, out var row)) runs.Add((record, row));
            return runs;
        }

        /// <summary>Every v2 or v3 line that counts, in file order; null when the file cannot be read, which is logged.</summary>
        internal static List<RunRecord> ReadRecords(string path)
        {
            if (!File.Exists(path)) return new List<RunRecord>();

            string[] lines;
            try
            {
                lines = File.ReadAllLines(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Debug.LogError($"Run records not read from '{path}': {e.Message}");
                return null;
            }

            var records = new List<RunRecord>(lines.Length);
            foreach (var line in lines)
                if (TryParse(line, out var record)) records.Add(record);
            return records;
        }

        private static bool TryParse(string line, out RunRecord record)
        {
            record = default;
            // JsonUtility unboxes an empty line to a null struct, which throws NullReferenceException.
            if (string.IsNullOrWhiteSpace(line)) return false;

            try
            {
                record = JsonUtility.FromJson<RunRecord>(line);
            }
            catch (ArgumentException)
            {
                return false;
            }
            return RunRecord.Counts(record.schema, record.outcome);
        }

        private static bool TryRead(in RunRecord record, out Row row)
        {
            row = default;
            if (!RunRecord.Counts(record.schema, record.outcome)) return false;
            if (!DateTime.TryParseExact(record.endedUtc, RunRecord.EndedUtcFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var ended))
                return false;

            row = new Row(record.sectorsCleared, record.kills, record.secondsSurvived, ended, record.player);
            return true;
        }
    }
}

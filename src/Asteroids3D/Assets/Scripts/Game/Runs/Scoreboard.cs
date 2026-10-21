using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Game.Runs
{
    /// <summary>
    /// Every multi-sector run in the run-record store, best first, and where this run places among them.
    /// Only <see cref="RunRecord.SchemaId"/> lines count, whatever their stat fingerprint; v1 lines and lines
    /// that do not parse stay in the file and off the board. <see cref="Compare"/> is the whole ranking rule,
    /// so a scoring system replaces that one method. The rule and what the board shows:
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

            public Row(int sectorsCleared, int kills, float secondsSurvived, DateTime endedUtc)
            {
                SectorsCleared = sectorsCleared;
                Kills = kills;
                SecondsSurvived = secondsSurvived;
                EndedUtc = endedUtc;
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
                    : throw new ArgumentException("This run's record is not a readable run-record-v2.", nameof(thisRun));

            if (!File.Exists(path)) return new Scoreboard(Array.Empty<Row>(), mine);

            string[] lines;
            try
            {
                lines = File.ReadAllLines(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Debug.LogError($"Scoreboard not read from '{path}': {e.Message}");
                return new Scoreboard(Array.Empty<Row>(), null);
            }

            var recorded = new List<Row>(lines.Length);
            foreach (var line in lines)
                if (TryParse(line, out var row)) recorded.Add(row);
            return new Scoreboard(recorded, mine);
        }

        private static bool TryParse(string line, out Row row)
        {
            row = default;
            // JsonUtility unboxes an empty line to a null struct, which throws NullReferenceException.
            if (string.IsNullOrWhiteSpace(line)) return false;

            RunRecord record;
            try
            {
                record = JsonUtility.FromJson<RunRecord>(line);
            }
            catch (ArgumentException)
            {
                return false;
            }
            return TryRead(record, out row);
        }

        private static bool TryRead(in RunRecord record, out Row row)
        {
            row = default;
            if (record.schema != RunRecord.SchemaId) return false;
            if (!DateTime.TryParseExact(record.endedUtc, RunRecord.EndedUtcFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var ended))
                return false;

            row = new Row(record.sectorsCleared, record.kills, record.secondsSurvived, ended);
            return true;
        }
    }
}

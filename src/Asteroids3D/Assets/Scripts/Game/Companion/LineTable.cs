using System;
using System.Collections.Generic;
using System.Text;

namespace Game.Companion
{
    /// <summary>
    /// The parsed line file, the canonical CSV in the repo.
    /// Only In game rows are kept. A bad row fails the parse by name.
    /// A pick is an exchange: an opening line and sometimes a reply.
    /// The arc and its rulings: https://github.com/amindell11/astronomical-home/issues/1321
    /// </summary>
    public sealed class LineTable
    {
        public const string CompanionSpeaker = "Companion";

        private const string AfterAny = "any";
        private const string AfterEither = "either";

        private readonly List<Row> rows;
        private readonly string[] factions;

        private LineTable(List<Row> rows)
        {
            this.rows = rows;
            factions = FactionsOf(rows);
        }

        public IReadOnlyCollection<string> Factions => factions;

        public static LineTable Parse(string csv, IReadOnlyList<LineSlot> slots)
        {
            if (csv == null) throw new ArgumentNullException(nameof(csv));
            if (slots == null) throw new ArgumentNullException(nameof(slots));

            var records = Records(csv);
            var header = records[0];
            var idColumn = Column(header, "ID");
            var momentColumn = Column(header, "Moment");
            var factionColumn = Column(header, "Faction");
            var speakerColumn = Column(header, "Speaker");
            var afterColumn = Column(header, "After");
            var lineColumn = Column(header, "Line");
            var statusColumn = Column(header, "Status");

            var kept = new List<Row>();
            var seen = new HashSet<string>();
            for (var i = 1; i < records.Count; i++)
            {
                var number = i;
                var cells = records[i];
                if (cells.Count != header.Count)
                    throw new FormatException(
                        $"Line file row {number} has {cells.Count} cells; the header has {header.Count}.");

                var id = cells[idColumn];
                if (id.Length == 0)
                    throw new FormatException($"Line file row {number} has no ID.");
                if (!seen.Add(id))
                    throw new FormatException($"Line file ID '{id}' appears twice.");

                var status = cells[statusColumn];
                if (status == "Later" || status == "Draft" || status == "Cut") continue;
                if (status != "In game")
                    throw new FormatException(
                        $"Line '{id}' has status '{status}'; expected In game, Later, Draft or Cut.");

                var moment = cells[momentColumn];
                var knownMoment = false;
                for (var s = 0; s < slots.Count; s++)
                {
                    if (slots[s].Moment != moment) continue;
                    knownMoment = true;
                    break;
                }

                if (!knownMoment)
                    throw new FormatException($"Line '{id}' names moment '{moment}', which has no line slot.");

                var speaker = cells[speakerColumn];
                if (string.IsNullOrWhiteSpace(speaker))
                    throw new FormatException($"Line '{id}' has no speaker.");

                var text = cells[lineColumn];
                if (string.IsNullOrWhiteSpace(text))
                    throw new FormatException($"Line '{id}' has no text.");

                kept.Add(new Row(id, moment, cells[factionColumn], speaker, cells[afterColumn], text));
            }

            Validate(kept);
            return new LineTable(kept);
        }

        public IReadOnlyList<SpokenLine> Pick(string moment, IReadOnlyDictionary<string, string> facts,
            Func<int, int> range, bool withReply)
        {
            facts.TryGetValue("faction", out var faction);
            var openers = new List<Row>();
            foreach (var row in rows)
            {
                if (row.Moment != moment || !row.CanOpen || !MatchesFaction(row, faction)) continue;
                openers.Add(row);
            }

            if (openers.Count == 0) return Array.Empty<SpokenLine>();

            var opener = openers[range(openers.Count)];
            if (!withReply) return new[] { Spoken(opener) };

            var followers = new List<Row>();
            foreach (var row in rows)
            {
                if (!row.CanFollow(opener) || !MatchesFaction(row, faction)) continue;
                followers.Add(row);
            }

            if (followers.Count == 0) return new[] { Spoken(opener) };
            return new[] { Spoken(opener), Spoken(followers[range(followers.Count)]) };
        }

        private static void Validate(List<Row> kept)
        {
            foreach (var row in kept)
            {
                if (NamesRow(row.After))
                {
                    var found = false;
                    var target = default(Row);
                    foreach (var other in kept)
                    {
                        if (other.Id != row.After) continue;
                        target = other;
                        found = true;
                        break;
                    }

                    if (!found)
                        throw new FormatException(
                            $"Line '{row.Id}' follows '{row.After}', which is not an In game line.");
                    if (target.Moment != row.Moment)
                        throw new FormatException(
                            $"Line '{row.Id}' follows '{row.After}', which speaks for another moment.");
                    if (target.Speaker == row.Speaker)
                        throw new FormatException(
                            $"Line '{row.Id}' follows '{row.After}', which has the same speaker.");
                    if (!target.CanOpen || !row.CanFollow(target))
                        throw new FormatException(
                            $"Line '{row.Id}' follows '{row.After}', which can never open for it.");
                }

                if (row.After != AfterAny || HasOpener(kept, row)) continue;
                throw new FormatException(
                    $"Line '{row.Id}' follows an opening line from another speaker, and its moment has none.");
            }
        }

        private static bool HasOpener(List<Row> kept, Row follower)
        {
            foreach (var row in kept)
                if (row.CanOpen && follower.CanFollow(row))
                    return true;
            return false;
        }

        private static bool NamesRow(string after) =>
            after.Length != 0 && after != AfterAny && after != AfterEither;

        private static bool MatchesFaction(Row row, string faction) =>
            row.Faction.Length == 0 || row.Faction == faction;

        private static SpokenLine Spoken(Row row) => new SpokenLine(row.Speaker, row.Text);

        private static List<List<string>> Records(string csv)
        {
            var records = new List<List<string>>();
            var record = new List<string>();
            var field = new StringBuilder();
            var quoted = false;
            var fieldQuoted = false;

            void AddField()
            {
                record.Add(fieldQuoted ? field.ToString() : field.ToString().Trim());
                field.Clear();
                fieldQuoted = false;
            }

            void EndRecord()
            {
                if (record.Count == 0 && !fieldQuoted && field.ToString().Trim().Length == 0)
                {
                    field.Clear();
                    return;
                }

                AddField();
                records.Add(record);
                record = new List<string>();
            }

            var i = 0;
            while (i < csv.Length)
            {
                var c = csv[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '"')
                        {
                            field.Append('"');
                            i += 2;
                            continue;
                        }

                        quoted = false;
                        i++;
                        continue;
                    }

                    field.Append(c);
                    i++;
                    continue;
                }

                if (c == ',')
                {
                    AddField();
                    i++;
                    continue;
                }

                if (c == '\r' || c == '\n')
                {
                    if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                    i++;
                    EndRecord();
                    continue;
                }

                if (c == '"' && field.Length == 0)
                {
                    quoted = true;
                    fieldQuoted = true;
                    i++;
                    continue;
                }

                field.Append(c);
                i++;
            }

            if (quoted) throw new FormatException("The line file has an unclosed quote.");

            EndRecord();
            if (records.Count == 0) throw new FormatException("The line file has no header row.");
            return records;
        }

        private static int Column(List<string> header, string name)
        {
            for (var i = 0; i < header.Count; i++)
                if (header[i] == name) return i;
            throw new FormatException($"The line file's table has no '{name}' column.");
        }

        private static string[] FactionsOf(List<Row> rows)
        {
            var factions = new List<string>();
            foreach (var row in rows)
            {
                if (row.Faction.Length == 0 || factions.Contains(row.Faction)) continue;
                factions.Add(row.Faction);
            }

            return factions.ToArray();
        }

        private readonly struct Row
        {
            public Row(string id, string moment, string faction, string speaker, string after, string text)
            {
                Id = id;
                Moment = moment;
                Faction = faction;
                Speaker = speaker;
                After = after;
                Text = text;
            }

            public string Id { get; }
            public string Moment { get; }
            public string Faction { get; }
            public string Speaker { get; }
            public string After { get; }
            public string Text { get; }

            public bool CanOpen => After.Length == 0 || After == AfterEither;

            public bool CanFollow(Row opener) =>
                Moment == opener.Moment
                && Speaker != opener.Speaker
                && (Faction.Length == 0 || opener.Faction.Length == 0 || Faction == opener.Faction)
                && (After == AfterAny || After == AfterEither || After == opener.Id);
        }
    }
}

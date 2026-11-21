using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Game.Companion
{
    /// <summary>
    /// The parsed line file, the export of the lines doc's Line sheet tab.
    /// Only In game rows are kept. A bad row fails the parse by name.
    /// The arc and its rulings: https://github.com/amindell11/astronomical-home/issues/1321
    /// </summary>
    public sealed class LineTable
    {
        public const string CompanionSpeaker = "Companion";

        private readonly List<Row> rows;
        private readonly string[] factions;

        private LineTable(List<Row> rows)
        {
            this.rows = rows;
            factions = FactionsOf(rows);
        }

        public IReadOnlyCollection<string> Factions => factions;

        public static LineTable Parse(string markdown, IReadOnlyList<LineSlot> slots)
        {
            if (markdown == null) throw new ArgumentNullException(nameof(markdown));
            if (slots == null) throw new ArgumentNullException(nameof(slots));

            var table = TableLines(markdown);
            var header = Cells(table[0]);
            var idColumn = Column(header, "ID");
            var momentColumn = Column(header, "Moment");
            var factionColumn = Column(header, "Faction");
            var speakerColumn = Column(header, "Speaker");
            var afterColumn = Column(header, "After");
            var lineColumn = Column(header, "Line");
            var statusColumn = Column(header, "Status");

            var kept = new List<Row>();
            var seen = new HashSet<string>();
            for (var i = 2; i < table.Count; i++)
            {
                var number = i - 1;
                var cells = Cells(table[i]);
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
                LineSlot slot = null;
                for (var s = 0; s < slots.Count; s++)
                {
                    if (slots[s].Moment != moment) continue;
                    slot = slots[s];
                    break;
                }

                if (slot == null)
                    throw new FormatException($"Line '{id}' names moment '{moment}', which has no line slot.");

                var speaker = cells[speakerColumn];
                var slotSpeaker = string.IsNullOrWhiteSpace(slot.Speaker) ? CompanionSpeaker : slot.Speaker;
                if (speaker != slotSpeaker)
                    throw new FormatException(
                        $"Line '{id}' is spoken by '{speaker}', but the '{moment}' line slot speaks as '{slotSpeaker}'.");

                var after = cells[afterColumn];
                if (after.Length > 0)
                    throw new FormatException(
                        $"Line '{id}' follows '{after}'; a line that follows another cannot play yet.");

                var text = cells[lineColumn];
                if (string.IsNullOrWhiteSpace(text))
                    throw new FormatException($"Line '{id}' has no text.");

                kept.Add(new Row(moment, cells[factionColumn], text));
            }

            return new LineTable(kept);
        }

        public string Pick(string moment, IReadOnlyDictionary<string, string> facts, Func<int, int> range)
        {
            facts.TryGetValue("faction", out var faction);
            var matches = new List<string>();
            foreach (var row in rows)
            {
                if (row.Moment != moment) continue;
                if (row.Faction.Length != 0 && row.Faction != faction) continue;
                matches.Add(row.Text);
            }

            if (matches.Count == 0) return null;
            return matches[range(matches.Count)];
        }

        private static List<string> TableLines(string markdown)
        {
            var split = markdown.Split('\n');
            var lines = new string[split.Length];
            for (var i = 0; i < split.Length; i++)
                lines[i] = split[i].TrimEnd('\r').Trim();

            var start = -1;
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith("|", StringComparison.Ordinal)) continue;
                start = i;
                break;
            }

            if (start < 0) throw new FormatException("The line file has no table.");

            var table = new List<string>();
            for (var i = start; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith("|", StringComparison.Ordinal)) break;
                table.Add(lines[i]);
            }

            return table;
        }

        private static List<string> Cells(string line)
        {
            var parts = new List<string>();
            var start = 0;
            for (var i = 0; i < line.Length; i++)
            {
                if (line[i] != '|' || (i > 0 && line[i - 1] == '\\')) continue;
                parts.Add(line.Substring(start, i - start));
                start = i + 1;
            }

            parts.Add(line.Substring(start));
            if (parts.Count > 0) parts.RemoveAt(parts.Count - 1);
            if (parts.Count > 0) parts.RemoveAt(0);

            for (var i = 0; i < parts.Count; i++)
                parts[i] = Unescape(parts[i].Trim());
            return parts;
        }

        private static string Unescape(string cell)
        {
            var builder = new StringBuilder(cell.Length);
            for (var i = 0; i < cell.Length; i++)
            {
                if (cell[i] == '\\' && i + 1 < cell.Length)
                {
                    builder.Append(cell[i + 1]);
                    i++;
                    continue;
                }

                builder.Append(cell[i]);
            }

            return WebUtility.HtmlDecode(builder.ToString());
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
            public Row(string moment, string faction, string text)
            {
                Moment = moment;
                Faction = faction;
                Text = text;
            }

            public string Moment { get; }
            public string Faction { get; }
            public string Text { get; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ships;
using UnityEngine;
using RL.Episodes;
using RL.Hosts.Lanes;

namespace RL.Probes
{
    [Serializable]
    public struct MatrixRow
    {
        public string schema;
        public int seed;
        public int episodeIndex;
        public string pair;
        public bool swapped;
        public string winner;
        public float simSeconds;
        public MarksmanshipTally tallyA;
        public MarksmanshipTally tallyB;

        public const string SchemaId = "matrix-v1";

        public string ToJsonLine() => JsonUtility.ToJson(this);
    }

    /// <summary>One unordered pair's episodes. A's win rate counts a draw as half a win. Time to kill is the mean sim time of the fights that named a winner.</summary>
    [Serializable]
    public struct MatrixPairSummary
    {
        public string pair;
        public string weaponA;
        public string weaponB;
        public int episodes;
        public float aWinRate;
        public int decidedFights;
        public float meanTimeToKill;
    }

    /// <summary>One weapon across the fights it appeared in. The win rate leaves out fights against itself. Hit rate and damage per second pool every side, self fights included.</summary>
    [Serializable]
    public struct MatrixWeaponSummary
    {
        public string weapon;
        public int againstOthers;
        public float winRateAgainstOthers;
        public int fired;
        public int hits;
        public float hitRate;
        public float damageDealt;
        public float simSeconds;
        public float damagePerSecond;
    }

    /// <summary>The armed-matrix instrument: one marksmanship sampler on each ship, a winner read from which ship is still alive, and a sidecar of pair and weapon rates. The markdown table beside the sidecar is those same numbers.</summary>
    public sealed class MatrixProbe : IHarnessProbe
    {
        public const string ProbeName = "matrix";
        public const string SummarySchemaId = "matrix-summary-v1";
        public const string SideA = "A";
        public const string SideB = "B";
        public const string Draw = "draw";

        [Serializable]
        public struct Sidecar
        {
            public string schema;
            public MatrixPairSummary[] pairs;
            public MatrixWeaponSummary[] weapons;
        }

        private readonly List<MatrixRow> rows = new();
        private MarksmanshipSampler samplerA;
        private MarksmanshipSampler samplerB;
        private EpisodePair pair;
        private string label;
        private int seed;
        private int episodeIndex;
        private bool swapped;

        public string Name => ProbeName;

        /// <summary>The table rides beside the summary sidecar, under a name no summary glob matches.</summary>
        public static string TablePath(string summaryPath) => Path.ChangeExtension(summaryPath, ".md");

        public static string Winner(bool aAlive, bool bAlive)
        {
            if (aAlive == bAlive) return Draw;
            return aAlive ? SideA : SideB;
        }

        /// <summary>A's share of the fight. A draw is half. Anything that is not a side's win is a draw.</summary>
        public static float SideScore(string winner, bool sideIsA)
        {
            if (winner == SideA) return sideIsA ? 1f : 0f;
            if (winner == SideB) return sideIsA ? 0f : 1f;
            return 0.5f;
        }

        public static Sidecar Score(IReadOnlyList<MatrixRow> source)
        {
            var order = new List<string>();
            var byPair = new Dictionary<string, List<MatrixRow>>();
            foreach (var row in source)
            {
                if (!byPair.TryGetValue(row.pair, out var list))
                {
                    list = new List<MatrixRow>();
                    byPair[row.pair] = list;
                    order.Add(row.pair);
                }
                list.Add(row);
            }

            var pairs = new MatrixPairSummary[order.Count];
            for (var i = 0; i < order.Count; i++)
                pairs[i] = ScorePair(order[i], byPair[order[i]]);
            return new Sidecar
            {
                schema = SummarySchemaId,
                pairs = pairs,
                weapons = ScoreWeapons(source, pairs),
            };
        }

        public void Begin(in ProbeContext context)
        {
            label = context.opponentLabel;
            seed = context.spec.runSeed;
            episodeIndex = context.episodeIndex;
            swapped = MatrixLane.SidesSwapped(episodeIndex);
            pair = context.pair;
            samplerA = new MarksmanshipSampler(pair.Agent, pair.Baseline);
            samplerB = new MarksmanshipSampler(pair.Baseline, pair.Agent);
        }

        public void Sample()
        {
            samplerA.Sample();
            samplerB.Sample();
        }

        public string End(in EpisodeResult result)
        {
            var row = new MatrixRow
            {
                schema = MatrixRow.SchemaId,
                seed = seed,
                episodeIndex = episodeIndex,
                pair = label,
                swapped = swapped,
                winner = Winner(Alive(pair.Agent), Alive(pair.Baseline)),
                simSeconds = result.simSeconds,
                tallyA = samplerA.Tally,
                tallyB = samplerB.Tally,
            };
            rows.Add(row);
            ReleaseSamplers();
            return row.ToJsonLine();
        }

        public void Summarize(string summaryPath)
        {
            var sidecar = Score(rows);
            File.WriteAllText(summaryPath, JsonUtility.ToJson(sidecar, prettyPrint: true));
            File.WriteAllText(TablePath(summaryPath), MarkdownTable(sidecar));
        }

        public void Dispose() => ReleaseSamplers();

        internal static string MarkdownTable(Sidecar sidecar)
        {
            var names = new string[sidecar.weapons.Length];
            for (var i = 0; i < names.Length; i++) names[i] = sidecar.weapons[i].weapon;

            var table = new StringBuilder();
            table.Append("## Win rate of row vs column (draw = 0.5), n per cell\n\n");
            table.Append("| row \\ col | ");
            table.Append(string.Join(" | ", names));
            table.Append(" | mean vs others |\n");
            table.Append('|');
            for (var i = 0; i < names.Length + 2; i++) table.Append("---|");
            table.Append('\n');
            for (var r = 0; r < names.Length; r++)
            {
                var cells = new string[names.Length];
                for (var c = 0; c < names.Length; c++)
                    cells[c] = Cell(sidecar.pairs, names[r], names[c]);
                var weapon = sidecar.weapons[r];
                var mean = weapon.againstOthers > 0 ? Number(weapon.winRateAgainstOthers, "0.00") : "-";
                table.Append("| ").Append(names[r]).Append(" | ").Append(string.Join(" | ", cells))
                    .Append(" | ").Append(mean).Append(" |\n");
            }

            table.Append("\n## Per pair\n\n");
            table.Append("| Pair | Episodes | A win rate | Decided | Mean TTK (s) |\n");
            table.Append("|---|--:|--:|--:|--:|\n");
            foreach (var pairSummary in sidecar.pairs)
            {
                var ttk = pairSummary.decidedFights > 0 ? Number(pairSummary.meanTimeToKill, "0.0") : "";
                table.Append("| ").Append(pairSummary.pair).Append(" | ").Append(Count(pairSummary.episodes))
                    .Append(" | ").Append(Number(pairSummary.aWinRate, "0.00")).Append(" | ")
                    .Append(Count(pairSummary.decidedFights)).Append(" | ").Append(ttk).Append(" |\n");
            }

            table.Append("\n## Per weapon\n\n");
            table.Append("| Weapon | Win vs others | Hit % | DPS |\n");
            table.Append("|---|--:|--:|--:|\n");
            foreach (var weapon in sidecar.weapons)
            {
                var win = weapon.againstOthers > 0 ? Number(weapon.winRateAgainstOthers, "0.00") : "";
                var hit = weapon.fired > 0 ? Number(100f * weapon.hitRate, "0.0") : "";
                table.Append("| ").Append(weapon.weapon).Append(" | ").Append(win).Append(" | ").Append(hit)
                    .Append(" | ").Append(Number(weapon.damagePerSecond, "0.0")).Append(" |\n");
            }
            return table.ToString();
        }

        private static MatrixPairSummary ScorePair(string pair, List<MatrixRow> pairRows)
        {
            var score = 0f;
            var decided = 0;
            var killSeconds = 0f;
            foreach (var row in pairRows)
            {
                score += SideScore(row.winner, sideIsA: true);
                if (row.winner != SideA && row.winner != SideB) continue;
                decided++;
                killSeconds += row.simSeconds;
            }
            return new MatrixPairSummary
            {
                pair = pair,
                weaponA = pairRows[0].tallyA.weapon,
                weaponB = pairRows[0].tallyB.weapon,
                episodes = pairRows.Count,
                aWinRate = score / pairRows.Count,
                decidedFights = decided,
                meanTimeToKill = decided > 0 ? killSeconds / decided : 0f,
            };
        }

        private struct WeaponAccum
        {
            public int againstOthers;
            public float score;
            public int fired;
            public int hits;
            public float damage;
            public float simSeconds;
        }

        private static MatrixWeaponSummary[] ScoreWeapons(IReadOnlyList<MatrixRow> source, MatrixPairSummary[] pairs)
        {
            var order = new List<string>();
            foreach (var pairSummary in pairs)
            {
                AddWeapon(order, pairSummary.weaponA);
                AddWeapon(order, pairSummary.weaponB);
            }
            var accum = new Dictionary<string, WeaponAccum>();
            foreach (var name in order) accum[name] = default;
            foreach (var row in source)
            {
                AddSide(accum, row.tallyA, row.tallyB.weapon, row.winner, sideIsA: true, row.simSeconds);
                AddSide(accum, row.tallyB, row.tallyA.weapon, row.winner, sideIsA: false, row.simSeconds);
            }

            var weapons = new MatrixWeaponSummary[order.Count];
            for (var i = 0; i < order.Count; i++)
            {
                var name = order[i];
                var g = accum[name];
                weapons[i] = new MatrixWeaponSummary
                {
                    weapon = name,
                    againstOthers = g.againstOthers,
                    winRateAgainstOthers = g.againstOthers > 0 ? g.score / g.againstOthers : 0f,
                    fired = g.fired,
                    hits = g.hits,
                    hitRate = g.fired > 0 ? (float)g.hits / g.fired : 0f,
                    damageDealt = g.damage,
                    simSeconds = g.simSeconds,
                    damagePerSecond = g.simSeconds > 0f ? g.damage / g.simSeconds : 0f,
                };
            }
            return weapons;
        }

        private static void AddSide(Dictionary<string, WeaponAccum> accum, MarksmanshipTally tally, string opponent,
            string winner, bool sideIsA, float simSeconds)
        {
            if (!accum.TryGetValue(tally.weapon, out var g)) return;
            g.fired += tally.fired;
            g.hits += tally.hits;
            g.damage += tally.damageDealt;
            g.simSeconds += simSeconds;
            if (tally.weapon != opponent)
            {
                g.againstOthers++;
                g.score += SideScore(winner, sideIsA);
            }
            accum[tally.weapon] = g;
        }

        private static void AddWeapon(List<string> order, string name)
        {
            if (!order.Contains(name)) order.Add(name);
        }

        private static string Cell(MatrixPairSummary[] pairs, string rowWeapon, string columnWeapon)
        {
            foreach (var pairSummary in pairs)
            {
                if (pairSummary.weaponA == rowWeapon && pairSummary.weaponB == columnWeapon)
                    return Counted(pairSummary.aWinRate, pairSummary.episodes);
                if (rowWeapon != columnWeapon
                    && pairSummary.weaponA == columnWeapon && pairSummary.weaponB == rowWeapon)
                    return Counted(1f - pairSummary.aWinRate, pairSummary.episodes);
            }
            return "-";
        }

        private static string Counted(float rate, int episodes) =>
            Number(rate, "0.00") + " (" + Count(episodes) + ")";

        private static bool Alive(Ship ship) => ship && ship.Damage.Health.CurrentValue > 0f;

        private void ReleaseSamplers()
        {
            samplerA?.Dispose();
            samplerB?.Dispose();
            samplerA = null;
            samplerB = null;
        }

        private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Number(float value, string format) =>
            value.ToString(format, CultureInfo.InvariantCulture);
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AI;
using AI.Tactics;
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
        public bool diedA;
        public bool diedB;
        public float aliveSecondsA;
        public float aliveSecondsB;
        public MarksmanshipTally tallyA;
        public MarksmanshipTally tallyB;
        public int enemies;
        public int enemiesKilled;
        public float damageTakenA;
        public float damageDealtA;
        public int stepsA;
        public int breakOffStepsA;
        public int regenStepsA;
        public int breakOffStepsEnemies;
        public int stepsEnemies;
        public bool timedOut;

        public const string SchemaId = "matrix-v3";

        public string ToJsonLine() => JsonUtility.ToJson(this);
    }

    /// <summary>One pair's episodes. A's win rate counts a draw as half a win. Time to kill is the mean sim time of the fights that named a winner. Damage per minute alive divides the damage that side dealt by the seconds it was alive. Mean seconds to die averages only the episodes that side died in. Damage taken per kill pools every episode's damage and kills. Break-off and regen shares count only the steps that side was alive.</summary>
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
        public float damagePerMinuteA;
        public int deathsA;
        public float meanSecondsToDieA;
        public float damagePerMinuteB;
        public int deathsB;
        public float meanSecondsToDieB;
        public int enemies;
        public float meanEnemiesKilled;
        public float damageTakenPerKillA;
        public float breakOffShareA;
        public float regenShareA;
        public float breakOffShareEnemies;
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

    /// <summary>The armed-matrix instrument: marksmanship samplers between side A and each enemy, a winner read from which side still has a ship alive, and a sidecar of pair rates, per-side damage per minute alive, and weapon rates. The markdown table beside the sidecar is those same numbers.</summary>
    public sealed class MatrixProbe : IHarnessProbe
    {
        public const string ProbeName = "matrix";
        public const string SummarySchemaId = "matrix-summary-v3";
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
        private MarksmanshipSampler[] dealtToExtras = Array.Empty<MarksmanshipSampler>();
        private MarksmanshipSampler[] takenFromExtras = Array.Empty<MarksmanshipSampler>();
        private EpisodePair pair;
        private Ship[] extras = Array.Empty<Ship>();
        private Brain referenceBrain;
        private Brain baselineBrain;
        private Brain[] extraBrains = Array.Empty<Brain>();
        private string label;
        private int seed;
        private int episodeIndex;
        private bool swapped;
        private int stepsA;
        private int breakOffStepsA;
        private int regenStepsA;
        private int breakOffStepsEnemies;
        private int stepsEnemies;
        private float shieldPrev;

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
            if (context.snapshots is not EpisodeLoopDriver driver)
                throw new InvalidOperationException("The matrix probe reads extra enemies from the episode driver.");
            extras = driver.ExtraOpponents;
            samplerA = new MarksmanshipSampler(pair.Agent, pair.Baseline);
            samplerB = new MarksmanshipSampler(pair.Baseline, pair.Agent);
            dealtToExtras = new MarksmanshipSampler[extras.Length];
            takenFromExtras = new MarksmanshipSampler[extras.Length];
            extraBrains = new Brain[extras.Length];
            for (var i = 0; i < extras.Length; i++)
            {
                dealtToExtras[i] = new MarksmanshipSampler(pair.Agent, extras[i]);
                takenFromExtras[i] = new MarksmanshipSampler(extras[i], pair.Agent);
                extraBrains[i] = BrainOf(extras[i]);
            }
            referenceBrain = BrainOf(pair.Agent);
            baselineBrain = BrainOf(pair.Baseline);
            stepsA = 0;
            breakOffStepsA = 0;
            regenStepsA = 0;
            breakOffStepsEnemies = 0;
            stepsEnemies = 0;
            shieldPrev = pair.Agent.Damage.Shield.CurrentValue;
        }

        public void Sample()
        {
            samplerA.Sample();
            samplerB.Sample();
            for (var i = 0; i < dealtToExtras.Length; i++)
            {
                dealtToExtras[i].Sample();
                takenFromExtras[i].Sample();
            }

            if (Alive(pair.Agent))
            {
                stepsA++;
                if (BreakingOff(referenceBrain)) breakOffStepsA++;
                var shield = pair.Agent.Damage.Shield.CurrentValue;
                if (shield > shieldPrev) regenStepsA++;
                shieldPrev = shield;
            }

            CountEnemy(pair.Baseline, baselineBrain);
            for (var i = 0; i < extras.Length; i++)
                CountEnemy(extras[i], extraBrains[i]);
        }

        public string End(in EpisodeResult result)
        {
            var aAlive = Alive(pair.Agent);
            var bAlive = Alive(pair.Baseline);
            var dealt = samplerA.Tally.damageDealt;
            var taken = samplerB.Tally.damageDealt;
            for (var i = 0; i < dealtToExtras.Length; i++)
            {
                dealt += dealtToExtras[i].Tally.damageDealt;
                taken += takenFromExtras[i].Tally.damageDealt;
            }
            var row = new MatrixRow
            {
                schema = MatrixRow.SchemaId,
                seed = seed,
                episodeIndex = episodeIndex,
                pair = label,
                swapped = swapped,
                winner = Winner(aAlive, AnyEnemyAlive(bAlive)),
                simSeconds = result.simSeconds,
                diedA = !aAlive,
                diedB = !bAlive,
                aliveSecondsA = result.simSeconds,
                aliveSecondsB = result.simSeconds,
                tallyA = samplerA.Tally,
                tallyB = samplerB.Tally,
                enemies = 1 + extras.Length,
                enemiesKilled = EnemiesKilled(bAlive),
                damageTakenA = taken,
                damageDealtA = dealt,
                stepsA = stepsA,
                breakOffStepsA = breakOffStepsA,
                regenStepsA = regenStepsA,
                breakOffStepsEnemies = breakOffStepsEnemies,
                stepsEnemies = stepsEnemies,
                timedOut = result.timedOut,
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
            table.Append("| Pair | Episodes | A win rate | Decided | Mean TTK (s) | A dmg/min | A deaths | A sec to die | B dmg/min | B deaths | B sec to die | Enemies | Mean killed | A dmg taken/kill | A break-off | A regen | Enemy break-off |\n");
            table.Append("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|\n");
            foreach (var pairSummary in sidecar.pairs)
            {
                var ttk = pairSummary.decidedFights > 0 ? Number(pairSummary.meanTimeToKill, "0.0") : "";
                var dieA = pairSummary.deathsA > 0 ? Number(pairSummary.meanSecondsToDieA, "0.0") : "";
                var dieB = pairSummary.deathsB > 0 ? Number(pairSummary.meanSecondsToDieB, "0.0") : "";
                var perKill = pairSummary.meanEnemiesKilled > 0f
                    ? Number(pairSummary.damageTakenPerKillA, "0.0") : "";
                table.Append("| ").Append(pairSummary.pair).Append(" | ").Append(Count(pairSummary.episodes))
                    .Append(" | ").Append(Number(pairSummary.aWinRate, "0.00")).Append(" | ")
                    .Append(Count(pairSummary.decidedFights)).Append(" | ").Append(ttk).Append(" | ")
                    .Append(Number(pairSummary.damagePerMinuteA, "0.0")).Append(" | ")
                    .Append(Count(pairSummary.deathsA)).Append(" | ").Append(dieA).Append(" | ")
                    .Append(Number(pairSummary.damagePerMinuteB, "0.0")).Append(" | ")
                    .Append(Count(pairSummary.deathsB)).Append(" | ").Append(dieB).Append(" | ")
                    .Append(Count(pairSummary.enemies)).Append(" | ")
                    .Append(Number(pairSummary.meanEnemiesKilled, "0.00")).Append(" | ").Append(perKill)
                    .Append(" | ").Append(Number(pairSummary.breakOffShareA, "0.00")).Append(" | ")
                    .Append(Number(pairSummary.regenShareA, "0.00")).Append(" | ")
                    .Append(Number(pairSummary.breakOffShareEnemies, "0.00")).Append(" |\n");
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
            var damageA = 0f;
            var damageB = 0f;
            var aliveA = 0f;
            var aliveB = 0f;
            var deathsA = 0;
            var deathsB = 0;
            var dieSecondsA = 0f;
            var dieSecondsB = 0f;
            var taken = 0f;
            var killed = 0;
            var breakOffA = 0;
            var stepsRef = 0;
            var regenA = 0;
            var breakOffEnemies = 0;
            var stepsEnemy = 0;
            foreach (var row in pairRows)
            {
                score += SideScore(row.winner, sideIsA: true);
                damageA += row.tallyA.damageDealt;
                damageB += row.tallyB.damageDealt;
                aliveA += row.aliveSecondsA;
                aliveB += row.aliveSecondsB;
                taken += row.damageTakenA;
                killed += row.enemiesKilled;
                breakOffA += row.breakOffStepsA;
                stepsRef += row.stepsA;
                regenA += row.regenStepsA;
                breakOffEnemies += row.breakOffStepsEnemies;
                stepsEnemy += row.stepsEnemies;
                if (row.diedA)
                {
                    deathsA++;
                    dieSecondsA += row.aliveSecondsA;
                }
                if (row.diedB)
                {
                    deathsB++;
                    dieSecondsB += row.aliveSecondsB;
                }
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
                damagePerMinuteA = aliveA > 0f ? damageA / aliveA * 60f : 0f,
                deathsA = deathsA,
                meanSecondsToDieA = deathsA > 0 ? dieSecondsA / deathsA : 0f,
                damagePerMinuteB = aliveB > 0f ? damageB / aliveB * 60f : 0f,
                deathsB = deathsB,
                meanSecondsToDieB = deathsB > 0 ? dieSecondsB / deathsB : 0f,
                enemies = pairRows[0].enemies,
                meanEnemiesKilled = (float)killed / pairRows.Count,
                damageTakenPerKillA = killed > 0 ? taken / killed : 0f,
                breakOffShareA = stepsRef > 0 ? (float)breakOffA / stepsRef : 0f,
                regenShareA = stepsRef > 0 ? (float)regenA / stepsRef : 0f,
                breakOffShareEnemies = stepsEnemy > 0 ? (float)breakOffEnemies / stepsEnemy : 0f,
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

        private static bool BreakingOff(Brain brain) => brain && brain.CurrentTactic == Tactic.BreakOff;

        private static Brain BrainOf(Ship ship)
        {
            var commander = ship.Commander as AICommander;
            return commander ? commander.Brain : null;
        }

        private void CountEnemy(Ship ship, Brain brain)
        {
            if (!Alive(ship)) return;
            stepsEnemies++;
            if (BreakingOff(brain)) breakOffStepsEnemies++;
        }

        private bool AnyEnemyAlive(bool baselineAlive)
        {
            if (baselineAlive) return true;
            for (var i = 0; i < extras.Length; i++)
                if (Alive(extras[i])) return true;
            return false;
        }

        private int EnemiesKilled(bool baselineAlive)
        {
            var killed = baselineAlive ? 0 : 1;
            for (var i = 0; i < extras.Length; i++)
                if (!Alive(extras[i])) killed++;
            return killed;
        }

        private void ReleaseSamplers()
        {
            samplerA?.Dispose();
            samplerB?.Dispose();
            samplerA = null;
            samplerB = null;
            DisposeSamplers(dealtToExtras);
            DisposeSamplers(takenFromExtras);
            dealtToExtras = Array.Empty<MarksmanshipSampler>();
            takenFromExtras = Array.Empty<MarksmanshipSampler>();
        }

        private static void DisposeSamplers(MarksmanshipSampler[] samplers)
        {
            for (var i = 0; i < samplers.Length; i++) samplers[i]?.Dispose();
        }

        private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Number(float value, string format) =>
            value.ToString(format, CultureInfo.InvariantCulture);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Game.Runs;
using UI.Recap;
using UnityEngine;

namespace Game.Preview
{
    /// <summary>
    /// The recap's fixture contexts, built from constants: every outcome at zero, fourteen and forty
    /// kills, run outcomes with the scoreboard rank inside and outside the top five. Index order is the
    /// panel's and the sheet's order.
    /// </summary>
    public static class RecapFixtures
    {
        private static readonly string[] Chassis = { "Crimson", "Valis", "Bulwark", "Nightshade", "Vanguard", "Junker_1" };
        private static readonly int[] KillCounts = { 0, 14, 40 };
        private static readonly DateTime Ended = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        private const int SectorCount = 3;

        private readonly struct Shape
        {
            public readonly RecapOutcome Outcome;
            public readonly int Kills;
            public readonly bool TopFive;

            public Shape(RecapOutcome outcome, int kills, bool topFive)
            {
                Outcome = outcome;
                Kills = kills;
                TopFive = topFive;
            }

            public string Name => Outcome == RecapOutcome.Extraction
                ? $"Extraction · {Kills} kills"
                : $"{Outcome} · {Kills} kills · rank {(TopFive ? "3 of 7" : "9 of 9")}";
        }

        private static readonly Shape[] Shapes = BuildShapes();

        public static IReadOnlyList<string> Names { get; } = Shapes.Select(shape => shape.Name).ToArray();

        public static RecapOutcomeContext Build(int index, Vector2 deathPoint)
        {
            var shape = Shapes[index];
            var kills = Enumerable.Range(0, shape.Kills).Select(i => Chassis[i % Chassis.Length]).ToArray();
            switch (shape.Outcome)
            {
                case RecapOutcome.Extraction:
                    return RecapOutcomeContext.Extraction(kills, 2, 222.8f);
                case RecapOutcome.Victory:
                    return RecapOutcomeContext.Victory(kills, SectorCount, SectorCount,
                        Board(shape.TopFive, new Scoreboard.Row(SectorCount, shape.Kills, 1234f, Ended)));
                case RecapOutcome.Defeat:
                    return RecapOutcomeContext.Defeat(kills, 1, SectorCount,
                        Board(shape.TopFive, new Scoreboard.Row(1, shape.Kills, 420f, Ended)), deathPoint);
                default:
                    throw new ArgumentOutOfRangeException(nameof(index), shape.Outcome, "Unknown recap outcome.");
            }
        }

        // Sectors rank first, then kills: the better rows always out-rank this run, zero-sector rows never.
        private static Scoreboard Board(bool topFive, Scoreboard.Row thisRun)
        {
            var better = topFive ? 2 : 8;
            var rows = new List<Scoreboard.Row>();
            for (var i = 0; i < better; i++)
                rows.Add(new Scoreboard.Row(SectorCount, 50 + i, 1500f + i, Ended.AddDays(-1 - i)));
            if (topFive)
                for (var i = 0; i < 4; i++)
                    rows.Add(new Scoreboard.Row(0, i, 100f + i, Ended.AddDays(-10 - i)));
            return new Scoreboard(rows, thisRun);
        }

        private static Shape[] BuildShapes()
        {
            var shapes = new List<Shape>();
            foreach (var outcome in new[] { RecapOutcome.Victory, RecapOutcome.Defeat })
                foreach (var kills in KillCounts)
                {
                    shapes.Add(new Shape(outcome, kills, true));
                    shapes.Add(new Shape(outcome, kills, false));
                }
            foreach (var kills in KillCounts)
                shapes.Add(new Shape(RecapOutcome.Extraction, kills, false));
            return shapes.ToArray();
        }
    }
}

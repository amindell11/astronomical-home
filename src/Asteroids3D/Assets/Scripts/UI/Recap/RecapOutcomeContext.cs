using System;
using System.Collections.Generic;
using Game.Runs;
using UnityEngine;

namespace UI.Recap
{
    public enum RecapOutcome { Extraction, Victory, Defeat }

    /// <summary>
    /// The immutable snapshot a recap presents. The game builds one from the run that just ended; a
    /// fixture builds one from constants. An extraction carries the sector just cleared and its
    /// completion time; a run outcome carries the sectors cleared and the scoreboard; a defeat also
    /// carries the plane point the player died at.
    /// </summary>
    public sealed class RecapOutcomeContext
    {
        public RecapOutcome Outcome { get; }
        public IReadOnlyList<string> Kills { get; }
        public int Sector { get; }
        public float CompletionSeconds { get; }
        public int SectorsCleared { get; }
        public int SectorCount { get; }
        public Scoreboard Board { get; }
        public Vector2 DeathPoint { get; }

        private RecapOutcomeContext(RecapOutcome outcome, IReadOnlyList<string> kills, int sector,
            float completionSeconds, int sectorsCleared, int sectorCount, Scoreboard board, Vector2 deathPoint)
        {
            Outcome = outcome;
            Kills = kills ?? throw new ArgumentNullException(nameof(kills));
            Sector = sector;
            CompletionSeconds = completionSeconds;
            SectorsCleared = sectorsCleared;
            SectorCount = sectorCount;
            Board = board;
            DeathPoint = deathPoint;
        }

        public static RecapOutcomeContext Extraction(IReadOnlyList<string> kills, int sector, float completionSeconds) =>
            new(RecapOutcome.Extraction, kills, sector, completionSeconds, 0, 0, null, Vector2.zero);

        public static RecapOutcomeContext Victory(IReadOnlyList<string> kills, int sectorsCleared, int sectorCount,
            Scoreboard board) =>
            new(RecapOutcome.Victory, kills, 0, 0f, sectorsCleared, sectorCount,
                board ?? throw new ArgumentNullException(nameof(board)), Vector2.zero);

        public static RecapOutcomeContext Defeat(IReadOnlyList<string> kills, int sectorsCleared, int sectorCount,
            Scoreboard board, Vector2 deathPoint) =>
            new(RecapOutcome.Defeat, kills, 0, 0f, sectorsCleared, sectorCount,
                board ?? throw new ArgumentNullException(nameof(board)), deathPoint);
    }
}

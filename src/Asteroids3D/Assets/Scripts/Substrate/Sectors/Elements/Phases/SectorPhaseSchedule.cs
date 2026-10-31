using System;

namespace Substrate.Sectors.Elements.Phases
{
    /// <summary>
    /// Where a sector phase table stands at an elapsed time: phases run in order after an opening
    /// lull, with a lull between each pair, and once the last phase ends the table is done and its
    /// last row stays in force. Pure time math, so the director's pacing is testable without a sector.
    /// </summary>
    public sealed class SectorPhaseSchedule
    {
        private readonly SectorPhase[] phases;
        private readonly float lullSeconds;
        private readonly float openingLullSeconds;

        public SectorPhaseSchedule(SectorPhase[] phases, float lullSeconds, float openingLullSeconds)
        {
            if (phases == null || phases.Length == 0)
                throw new ArgumentException("A sector phase table needs at least one phase.", nameof(phases));
            this.phases = phases;
            this.lullSeconds = lullSeconds;
            this.openingLullSeconds = openingLullSeconds;
        }

        public int Count => phases.Length;

        public SectorPhaseMark At(float elapsed)
        {
            var start = 0f;
            for (var i = 0; i < phases.Length; i++)
            {
                var lullEnd = start + (i == 0 ? openingLullSeconds : lullSeconds);
                if (elapsed < lullEnd) return new SectorPhaseMark(i, true, false, lullEnd - elapsed,
                    lullEnd - elapsed, elapsed - start);
                start = lullEnd;

                var end = start + phases[i].seconds;
                if (elapsed < end) return new SectorPhaseMark(i, false, false, end - elapsed,
                    end - elapsed + (i < phases.Length - 1 ? lullSeconds : 0f), elapsed - start);
                start = end;
            }

            return new SectorPhaseMark(phases.Length - 1, false, true, 0f, 0f, elapsed - start);
        }
    }

    public readonly struct SectorPhaseMark
    {
        // During a lull, Index names the upcoming sector phase.
        public int Index { get; }
        public bool InLull { get; }
        public bool Done { get; }
        public float SecondsLeft { get; }
        public float CountdownSeconds { get; }
        // Seconds into this sector phase or lull; once done, since the table finished.
        public float ElapsedSeconds { get; }

        public bool InOpeningLull => InLull && Index == 0;

        public SectorPhaseMark(int index, bool inLull, bool done, float secondsLeft, float countdownSeconds,
            float elapsedSeconds)
        {
            Index = index;
            InLull = inLull;
            Done = done;
            SecondsLeft = secondsLeft;
            CountdownSeconds = countdownSeconds;
            ElapsedSeconds = elapsedSeconds;
        }
    }
}

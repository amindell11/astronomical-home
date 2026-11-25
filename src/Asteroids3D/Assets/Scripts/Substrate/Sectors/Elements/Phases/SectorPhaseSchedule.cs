using System;

namespace Substrate.Sectors.Elements.Phases
{
    /// <summary>
    /// Where a sector phase table stands at an elapsed time: phases run in order after an opening
    /// lull, with a lull between each pair. A closing lull, when the sector has one, follows the last
    /// phase, and then the table is done and its last row stays in force. Pure time math, so the
    /// director's pacing is testable without a sector.
    /// </summary>
    public sealed class SectorPhaseSchedule
    {
        private readonly SectorPhase[] phases;
        private readonly float lullSeconds;
        private readonly float openingLullSeconds;
        private readonly float closingLullSeconds;
        private readonly float totalSeconds;

        public SectorPhaseSchedule(SectorPhase[] phases, float lullSeconds, float openingLullSeconds,
            float closingLullSeconds)
        {
            if (phases == null || phases.Length == 0)
                throw new ArgumentException("A sector phase table needs at least one phase.", nameof(phases));
            this.phases = phases;
            this.lullSeconds = lullSeconds;
            this.openingLullSeconds = openingLullSeconds;
            this.closingLullSeconds = closingLullSeconds;
            totalSeconds = openingLullSeconds + lullSeconds * (phases.Length - 1) + closingLullSeconds;
            foreach (var phase in phases)
                totalSeconds += phase.seconds;
        }

        public int Count => phases.Length;

        public SectorPhaseMark At(float elapsed)
        {
            var secondsToEnd = Math.Max(0f, totalSeconds - elapsed);
            var start = 0f;
            for (var i = 0; i < phases.Length; i++)
            {
                var lullEnd = start + (i == 0 ? openingLullSeconds : lullSeconds);
                if (elapsed < lullEnd) return new SectorPhaseMark(i, true, false, false, lullEnd - elapsed,
                    lullEnd - elapsed, elapsed - start, secondsToEnd);
                start = lullEnd;

                var end = start + phases[i].seconds;
                var following = i < phases.Length - 1 ? lullSeconds : closingLullSeconds;
                if (elapsed < end) return new SectorPhaseMark(i, false, false, false, end - elapsed,
                    end - elapsed + following, elapsed - start, secondsToEnd);
                start = end;
            }

            if (closingLullSeconds > 0f && elapsed < start + closingLullSeconds)
            {
                var left = start + closingLullSeconds - elapsed;
                return new SectorPhaseMark(phases.Length - 1, true, true, false, left, left,
                    elapsed - start, secondsToEnd);
            }

            return new SectorPhaseMark(phases.Length - 1, false, false, true, 0f, 0f,
                elapsed - start - closingLullSeconds, secondsToEnd);
        }
    }

    public readonly struct SectorPhaseMark
    {
        // A lull's Index names the upcoming phase. A closing lull names the last.
        public int Index { get; }
        public bool InLull { get; }
        public bool Closing { get; }
        public bool Done { get; }
        public float SecondsLeft { get; }
        public float CountdownSeconds { get; }
        // Seconds into this sector phase or lull; once done, since the table finished.
        public float ElapsedSeconds { get; }
        public float SecondsToEnd { get; }

        public bool InOpeningLull => InLull && Index == 0 && !Closing;

        public SectorPhaseMark(int index, bool inLull, bool closing, bool done, float secondsLeft,
            float countdownSeconds, float elapsedSeconds, float secondsToEnd)
        {
            Index = index;
            InLull = inLull;
            Closing = closing;
            Done = done;
            SecondsLeft = secondsLeft;
            CountdownSeconds = countdownSeconds;
            ElapsedSeconds = elapsedSeconds;
            SecondsToEnd = secondsToEnd;
        }
    }
}

using Unity.Mathematics;

namespace Movement.MPC
{
    /// <summary>
    /// Frame-to-frame gap choice with hysteresis. Associates this frame's gaps to the previously
    /// chosen one by angular proximity and keeps the previous choice unless a competitor beats it by
    /// a score margin. This is where chase oscillation is damped — at gap selection, not in the
    /// solver. Stateful; one per navigator.
    /// </summary>
    public sealed class GapSelector
    {
        private const float AssociationTol = 0.4f; // rad; how close a gap must be to be "the same" gap

        private bool hasChosen;
        private float chosenDir;

        public bool HasChosen => hasChosen;
        public Gap Chosen { get; private set; }

        public void Reset() => hasChosen = false;

        /// <summary>
        /// Picks a gap from <paramref name="gaps"/> (sorted best-first, <paramref name="count"/> valid).
        /// Returns false and clears state if there are none.
        /// </summary>
        public bool Select(Gap[] gaps, int count, float margin, out Gap chosen)
        {
            if (count <= 0)
            {
                hasChosen = false;
                chosen = default;
                return false;
            }

            var best = gaps[0]; // InsertTopK keeps index 0 as highest score
            if (!hasChosen)
            {
                Commit(best);
                chosen = best;
                return true;
            }

            // Associate the previous choice with this frame's nearest gap.
            var assoc = -1;
            var bestAng = AssociationTol;
            for (var i = 0; i < count; i++)
            {
                var da = math.abs(AngDelta(gaps[i].dirRad, chosenDir));
                if (da < bestAng) { bestAng = da; assoc = i; }
            }

            // Lost the old gap, or a competitor beats it by the margin → switch to the best.
            Gap pick;
            if (assoc < 0 || best.score > gaps[assoc].score * (1f + margin))
                pick = best;
            else
                pick = gaps[assoc];

            Commit(pick);
            chosen = pick;
            return true;
        }

        private void Commit(Gap g)
        {
            Chosen = g;
            chosenDir = g.dirRad;
            hasChosen = true;
        }

        private static float AngDelta(float a, float b)
        {
            const float twoPi = 2f * math.PI;
            var d = a - b;
            while (d > math.PI) d -= twoPi;
            while (d <= -math.PI) d += twoPi;
            return d;
        }
    }
}

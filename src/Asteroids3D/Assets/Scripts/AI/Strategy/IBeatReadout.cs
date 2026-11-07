namespace AI.Strategy
{
    /// <summary>A beat-picking brain's current beat and the scores of its last pick, for debug overlays that cannot see the brain's own assembly.</summary>
    public interface IBeatReadout
    {
        string Beat { get; }

        float BeatSecondsLeft { get; }

        /// <summary>How many beats the last pick scored; 0 before the first pick.</summary>
        int ScoredCount { get; }

        /// <summary>One scored beat of the last pick, in the brain's fixed scoring order.</summary>
        (string beat, float score) Scored(int index);
    }
}

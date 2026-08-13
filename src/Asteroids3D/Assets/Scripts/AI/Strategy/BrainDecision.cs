namespace AI
{
    /// <summary>One decision's three lanes. A transport, not a union — the commander routes each lane to its own consumer and keeps nothing. The fire lanes are strategic engage gates; the Gunner owns trigger timing.</summary>
    public readonly struct BrainDecision
    {
        public readonly NavObjective nav;
        public readonly bool engagePrimary;
        public readonly bool engageSecondary;
        public readonly bool boost;

        public BrainDecision(in NavObjective nav, bool engagePrimary = false,
            bool engageSecondary = false, bool boost = false)
        {
            this.nav = nav;
            this.engagePrimary = engagePrimary;
            this.engageSecondary = engageSecondary;
            this.boost = boost;
        }
    }
}

namespace AI.Strategy
{
    /// <summary>One decision's four lanes. A transport, not a union — the commander routes each lane to its own consumer and keeps nothing. The fire lanes are strategic engage gates; the Gunner owns trigger timing. Jump asks the ship to leave the sector.</summary>
    public readonly struct BrainDecision
    {
        public readonly NavObjective nav;
        public readonly bool engagePrimary;
        public readonly bool engageSecondary;
        public readonly bool boost;
        public readonly bool jump;

        public BrainDecision(in NavObjective nav, bool engagePrimary = false,
            bool engageSecondary = false, bool boost = false, bool jump = false)
        {
            this.nav = nav;
            this.engagePrimary = engagePrimary;
            this.engageSecondary = engageSecondary;
            this.boost = boost;
            this.jump = jump;
        }
    }
}

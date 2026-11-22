namespace AI.Strategy
{
    /// <summary>One decision's five lanes. A transport, not a union — the commander routes each lane to its own consumer and keeps nothing. The fire lanes are strategic engage gates; the Gunner owns trigger timing. Jump asks the ship to leave the sector. Recall asks for a lost enemy back.</summary>
    public readonly struct BrainDecision
    {
        public readonly NavObjective nav;
        public readonly bool engagePrimary;
        public readonly bool engageSecondary;
        public readonly bool boost;
        public readonly bool jump;
        public readonly bool recall;

        public BrainDecision(in NavObjective nav, bool engagePrimary = false,
            bool engageSecondary = false, bool boost = false, bool jump = false, bool recall = false)
        {
            this.nav = nav;
            this.engagePrimary = engagePrimary;
            this.engageSecondary = engageSecondary;
            this.boost = boost;
            this.jump = jump;
            this.recall = recall;
        }
    }
}

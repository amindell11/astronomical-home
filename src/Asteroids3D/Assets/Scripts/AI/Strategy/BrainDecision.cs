namespace AI
{
    /// <summary>One decision's three lanes. A transport, not a union — the commander routes each lane to its own consumer and keeps nothing.</summary>
    public readonly struct BrainDecision
    {
        public readonly NavObjective nav;
        public readonly FireControl primary;
        public readonly FireControl secondary;
        public readonly bool boost;

        public BrainDecision(in NavObjective nav, FireControl primary = default,
            FireControl secondary = default, bool boost = false)
        {
            this.nav = nav;
            this.primary = primary;
            this.secondary = secondary;
            this.boost = boost;
        }
    }
}

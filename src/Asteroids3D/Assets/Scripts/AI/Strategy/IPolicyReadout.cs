namespace AI
{
    /// <summary>One decision's commanded output in the enemy-anchored frame, as handed to <see cref="IIntentChooser.Decide"/>'s intent: a facing offset around the intercept anchor (with authority weight) and a polar velocity (radial/tangential speeds, with authority weight).</summary>
    public readonly struct PolicyAction
    {
        public readonly float facingOffsetRad;
        public readonly float facingWeight;
        public readonly float radialSpeed;
        public readonly float tangentialSpeed;
        public readonly float velocityWeight;

        public PolicyAction(float facingOffsetRad, float facingWeight, float radialSpeed,
            float tangentialSpeed, float velocityWeight)
        {
            this.facingOffsetRad = facingOffsetRad;
            this.facingWeight = facingWeight;
            this.radialSpeed = radialSpeed;
            this.tangentialSpeed = tangentialSpeed;
            this.velocityWeight = velocityWeight;
        }
    }

    /// <summary>Read-only access to a chooser's recent commanded actions, for debug gizmos to draw
    /// without coupling the render side to the decision policy's own state.</summary>
    public interface IPolicyReadout
    {
        /// <summary>Actions currently held, 0..capacity.</summary>
        int Count { get; }

        /// <summary>Monotonic decisions since the last reset — unlike <see cref="Count"/>, never saturates at the ring capacity.</summary>
        int TotalDecisions { get; }

        /// <summary>The i-th most recent action; 0 = newest, Count-1 = oldest.</summary>
        PolicyAction ActionFromNewest(int index);
    }
}

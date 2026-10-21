namespace AI.Strategy
{
    /// <summary>One decision's commanded output as carried by the nav objective <see cref="Brain.Decide"/> returns: the full intent sentence (facing offset, polar velocity, position offset/setpoint, lane and field weights, referent/frame branch choices) plus the trigger branches. Referent fields are the action's slot choices (0 = enemy, 1..N = rock slots), not entity identities — readouts correlate switches, they never resolve rocks.</summary>
    public readonly struct PolicyAction
    {
        public readonly float facingOffsetRad;
        public readonly float facingWeight;
        public readonly float radialSpeed;
        public readonly float tangentialSpeed;
        public readonly float velocityWeight;
        public readonly float posOffsetR;
        public readonly float posOffsetThetaRad;
        public readonly float posSetpoint;
        public readonly float posWeight;
        public readonly float laneWeight;
        public readonly float fieldWeight;
        public readonly int aimReferent;
        public readonly int posReferent;
        public readonly int velReferent;
        public readonly int posFrame;
        public readonly int velFrame;
        public readonly bool firePrimary;
        public readonly bool fireSecondary;
        public readonly bool boost;

        public PolicyAction(float facingOffsetRad, float facingWeight, float radialSpeed,
            float tangentialSpeed, float velocityWeight)
            : this(facingOffsetRad, facingWeight, radialSpeed, tangentialSpeed, velocityWeight,
                0f, 0f, 0f, 0f, 0f, 0f, 0, 0, 0, 0, 0, false, false, false) { }

        public PolicyAction(float facingOffsetRad, float facingWeight, float radialSpeed,
            float tangentialSpeed, float velocityWeight, float posOffsetR, float posOffsetThetaRad,
            float posSetpoint, float posWeight, float laneWeight, float fieldWeight,
            int aimReferent, int posReferent, int velReferent, int posFrame, int velFrame,
            bool firePrimary, bool fireSecondary, bool boost)
        {
            this.facingOffsetRad = facingOffsetRad;
            this.facingWeight = facingWeight;
            this.radialSpeed = radialSpeed;
            this.tangentialSpeed = tangentialSpeed;
            this.velocityWeight = velocityWeight;
            this.posOffsetR = posOffsetR;
            this.posOffsetThetaRad = posOffsetThetaRad;
            this.posSetpoint = posSetpoint;
            this.posWeight = posWeight;
            this.laneWeight = laneWeight;
            this.fieldWeight = fieldWeight;
            this.aimReferent = aimReferent;
            this.posReferent = posReferent;
            this.velReferent = velReferent;
            this.posFrame = posFrame;
            this.velFrame = velFrame;
            this.firePrimary = firePrimary;
            this.fireSecondary = fireSecondary;
            this.boost = boost;
        }
    }

    /// <summary>Read-only access to a brain's recent commanded actions, for debug gizmos and session
    /// probes to read without coupling to the decision policy's own state.</summary>
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

namespace Combat.Tethers
{
    public readonly struct TetherSpec
    {
        public readonly float Stiffness;
        public readonly float Damping;
        public readonly float ReelRate;
        public readonly float MinLength;
        public readonly float SnapForce;
        public readonly float MaxSeconds;

        public TetherSpec(float stiffness, float damping, float reelRate, float minLength, float snapForce, float maxSeconds)
        {
            Stiffness = stiffness;
            Damping = damping;
            ReelRate = reelRate;
            MinLength = minLength;
            SnapForce = snapForce;
            MaxSeconds = maxSeconds;
        }
    }
}

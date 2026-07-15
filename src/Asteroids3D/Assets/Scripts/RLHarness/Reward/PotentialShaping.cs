namespace Game.RLHarness
{
    /// <summary>Potential-based shaping (Ng et al. 1999). <see cref="Step"/> is the ONE place γ·Φ(s′) − Φ(s) is applied, and it forces terminal Φ = 0.</summary>
    public static class PotentialShaping
    {
        /// <summary>Φ_env(s) = k₁·[enemy in my firing envelope] − k₂·[I'm in the enemy's].</summary>
        public static float EnvelopePhi(in CombatSnapshot s, in RewardSpec spec) =>
            spec.envelopeK1 * (s.inMyEnvelope ? 1f : 0f)
            - spec.envelopeK2 * (s.inEnemyEnvelope ? 1f : 0f);

        /// <summary>Φ_border(s) = −k_b·ramp(r): zero inside softFraction·R, sharp rise toward R.</summary>
        public static float BorderPhi(in CombatSnapshot s, in RewardSpec spec) =>
            -spec.borderKb * BorderRamp(s.myDistFromCenter, spec.arenaRadius, spec.borderSoftFraction);

        /// <summary>Quadratic ramp: 0 at r ≤ softFraction·R, 1 at r = R, growing beyond.</summary>
        public static float BorderRamp(float r, float radius, float softFraction)
        {
            var rampStart = softFraction * radius;
            if (r <= rampStart || radius <= rampStart) return 0f;
            var x = (r - rampStart) / (radius - rampStart);
            return x * x;
        }

        /// <summary>One shaping contribution: γ·Φ(s′) − Φ(s), with Φ(s′) ≡ 0 on the terminal transition so the sum telescopes to −Φ(s₀) + (γ−1)·ΣΦ_mid.</summary>
        public static float Step(float phiPrev, float phiNext, float gamma, bool terminal) =>
            (terminal ? 0f : gamma * phiNext) - phiPrev;
    }
}

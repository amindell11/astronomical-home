namespace Game.RLHarness
{
    /// <summary>Potential-based shaping (Ng et al. 1999). <see cref="Step"/> is the ONE place γ·Φ(s′) − Φ(s) is applied, and it forces terminal Φ = 0.</summary>
    public static class PotentialShaping
    {
        /// <summary>Φ_env(s) = k₁·pursuit(distanceToTarget) − k₂·[I'm in the enemy's envelope]. The pursuit term is a continuous closing ramp gated on a live target; k₂ stays binary — a symmetric ramp would add a repulsion gradient and recreate mutual standoff.</summary>
        public static float EnvelopePhi(in CombatSnapshot s, in RewardSpec spec) =>
            spec.envelopeK1 * (s.enemyAlive ? PursuitRamp(s.distanceToTarget, s.fireDistance, spec.arenaRadius) : 0f)
            - spec.envelopeK2 * (s.inEnemyEnvelope ? 1f : 0f);

        /// <summary>Linear closing ramp: 1 at range ≤ fireDistance (saturated flat inside the envelope), 0 at range ≥ arenaRadius, linear between — monotone decreasing in distance, so closing raises Φ while saturation keeps close-range shaping from paying a dive to contact.</summary>
        public static float PursuitRamp(float distanceToTarget, float fireDistance, float arenaRadius)
        {
            if (distanceToTarget <= fireDistance) return 1f;
            if (distanceToTarget >= arenaRadius || arenaRadius <= fireDistance) return 0f;
            return (arenaRadius - distanceToTarget) / (arenaRadius - fireDistance);
        }

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

        /// <summary>One shaping contribution: Φ(s′) − Φ(s), UNDISCOUNTED, with Φ(s′) ≡ 0 on the terminal transition so the sum telescopes to exactly −Φ(s₀) + Φ_end.
        /// Deliberately not Ng's γ·Φ(s′) − Φ(s): at γ=0.99 that form leaks (γ−1)·Φ every decision, a drain proportional to how close the agent is. In episodes that end fast it is negligible (−0.56 over a 23s kill), but across a 600-decision stalemate it dominates (−1.57, 80% of the penalty) and pays roughly +2.0 to disengage from a target the agent cannot finish — measured 2026-07-25 as 150 consecutive Evader draws with a flat closing rate. Policy-invariance is traded for a pursuit gradient that points at the target.</summary>
        public static float Step(float phiPrev, float phiNext, bool terminal) =>
            (terminal ? 0f : phiNext) - phiPrev;
    }
}

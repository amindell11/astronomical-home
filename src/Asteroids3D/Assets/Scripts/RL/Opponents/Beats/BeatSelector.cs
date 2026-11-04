using System;
using UnityEngine;

namespace RL.Opponents.Beats
{
    internal readonly struct BeatSituation
    {
        public static readonly BeatSituation NoTarget = default;

        public readonly bool hasTarget;
        public readonly int attackers;
        public readonly float aimedAt;
        public readonly float shieldPct;
        public readonly bool hitRecently;
        public readonly bool flankReached;

        /// <param name="attackers">Allies already attacking the same target.</param>
        /// <param name="aimedAt">0 to 1: how squarely the target's nose points at this pilot.</param>
        public BeatSituation(int attackers, float aimedAt, float shieldPct, bool hitRecently, bool flankReached)
        {
            hasTarget = true;
            this.attackers = attackers;
            this.aimedAt = aimedAt;
            this.shieldPct = shieldPct;
            this.hitRecently = hitRecently;
            this.flankReached = flankReached;
        }
    }

    /// <summary>
    /// The utility pick over committed beats. One enemy's draw from its <see cref="Personality"/> scores
    /// each beat against the fight, and the best is flown for its own drawn length before anything is
    /// scored again; a new target picks at once. A beat ends early only past the minimum, and only on its
    /// own condition: a flank that reached the blind side, a break-off whose shield came back. Nothing
    /// re-scores mid-beat, so near-tied scores cannot flip a pilot back and forth.
    /// </summary>
    internal sealed class BeatSelector
    {
        // Ties keep the earlier beat here, so equal scores always pick the same beat.
        internal static readonly Beat[] ScoredBeats = { Beat.AttackRun, Beat.Flank, Beat.Hold, Beat.BreakOff };

        private const float UnhurriedBreakOff = 0.5f;

        private readonly Personality personality;
        private readonly System.Random rng;
        private readonly float[] scores = new float[ScoredBeats.Length];
        private float length = float.PositiveInfinity;

        internal BeatSelector(Personality personality, System.Random rng)
        {
            if (!personality) throw new ArgumentNullException(nameof(personality), "A beat pilot needs its personality.");
            this.personality = personality;
            this.rng = rng ?? throw new ArgumentNullException(nameof(rng));
            Attack = Vary(personality.attack);
            Flank = Vary(personality.flank);
            Hold = Vary(personality.hold);
            BreakOff = Vary(personality.breakOff);
            Crowding = Vary(personality.crowding);
        }

        internal float Attack { get; }
        internal float Flank { get; }
        internal float Hold { get; }
        internal float BreakOff { get; }
        internal float Crowding { get; }

        internal Beat Beat { get; private set; } = Beat.Search;
        internal float Elapsed { get; private set; }
        internal float SecondsLeft => Mathf.Max(0f, length - Elapsed);

        /// <summary>Picks made so far, so the brain can tell a fresh beat from a held one.</summary>
        internal int Picks { get; private set; }

        internal float LastScore(int index) => scores[index];

        internal void Advance(float seconds, in BeatSituation situation, bool targetChanged)
        {
            Elapsed += seconds;
            if (!situation.hasTarget)
            {
                if (Beat != Beat.Search) Enter(Beat.Search, float.PositiveInfinity);
                return;
            }
            if (Beat != Beat.Search && !targetChanged && !Ended(in situation)) return;
            Pick(in situation);
        }

        private float Score(Beat beat, in BeatSituation situation) => beat switch
        {
            Beat.AttackRun => Attack - Crowding * Excess(situation.attackers),
            Beat.Flank => Flank * (1f + situation.aimedAt) - Crowding * Excess(situation.attackers),
            Beat.Hold => Hold,
            Beat.BreakOff => BreakOff * LowShield(situation.shieldPct) * (situation.hitRecently ? 1f : UnhurriedBreakOff),
            _ => throw new ArgumentOutOfRangeException(nameof(beat), beat, "Search is flown without a target, never scored."),
        };

        private bool Ended(in BeatSituation situation)
        {
            if (Elapsed >= length) return true;
            if (Elapsed < personality.minBeatSeconds) return false;
            return Beat switch
            {
                Beat.Flank => situation.flankReached,
                Beat.BreakOff => situation.shieldPct >= personality.resumeShield,
                _ => false,
            };
        }

        private void Pick(in BeatSituation situation)
        {
            var best = 0;
            for (var i = 0; i < ScoredBeats.Length; i++)
            {
                scores[i] = Score(ScoredBeats[i], in situation);
                if (scores[i] > scores[best]) best = i;
            }
            var beat = ScoredBeats[best];
            Enter(beat, Draw(Lengths(beat)));
            Picks++;
        }

        private void Enter(Beat beat, float seconds)
        {
            Beat = beat;
            Elapsed = 0f;
            length = seconds;
        }

        private Vector2 Lengths(Beat beat) => beat switch
        {
            Beat.AttackRun => personality.attackSeconds,
            Beat.Flank => personality.flankSeconds,
            Beat.Hold => personality.holdSeconds,
            _ => personality.breakOffSeconds,
        };

        private int Excess(int allies) => Mathf.Max(0, allies - personality.tolerance);

        private float LowShield(float shieldPct) =>
            personality.breakOffShield > 0f ? Mathf.Clamp01(1f - shieldPct / personality.breakOffShield) : 0f;

        private float Vary(float value) => value * (1f + personality.variation * (2f * (float)rng.NextDouble() - 1f));

        private float Draw(Vector2 range) => range.x + (float)rng.NextDouble() * (range.y - range.x);
    }
}

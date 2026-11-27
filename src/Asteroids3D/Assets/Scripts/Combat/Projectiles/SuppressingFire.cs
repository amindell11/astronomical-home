using System;
using Ships.Status;

namespace Combat.Projectiles
{
    /// <summary>
    /// One gun's steady fire: once its last <c>hits</c> strikes on the same ship all land inside
    /// <c>window</c> seconds, every further strike keeps that ship suppressed for <c>hold</c> seconds.
    /// A strike on another ship starts the count over.
    /// </summary>
    public sealed class SuppressingFire
    {
        private readonly float[] struckAt;
        private readonly float window;
        private readonly float hold;
        private StatusEffects target;
        private int strikes;

        public SuppressingFire(int hits, float window, float hold)
        {
            if (hits < 1) throw new ArgumentOutOfRangeException(nameof(hits), hits, "Steady fire takes at least one hit.");
            struckAt = new float[hits];
            this.window = window;
            this.hold = hold;
        }

        public void Strike(StatusEffects struck, float now)
        {
            if (struck != target)
            {
                target = struck;
                strikes = 0;
            }
            struckAt[strikes++ % struckAt.Length] = now;
            // The slot written next holds the oldest of the last hits strikes.
            if (strikes >= struckAt.Length && now - struckAt[strikes % struckAt.Length] <= window)
                struck.Apply(StatusEffect.Suppressed, hold);
        }
    }
}

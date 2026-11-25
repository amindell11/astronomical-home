using System.Collections.Generic;
using AI.Context;
using Movement;
using UnityEngine;
using Utils;

namespace AI.Gunnery
{
    /// <summary>The target as the Gunner sees it: late by the reaction delay, then turned about the shooter by the wander and by earned accuracy's error. Everything the Gunner aims from reads this perceived target, so it feeds the trigger, the aim reference and, through that, the MPC's facing; movement keeps the true target. Ruled on #1057.</summary>
    internal sealed class AimPerception
    {
        private const uint WanderStream = 1;
        private const uint EarnedStream = 2;

        private readonly AimSettings settings;
        private readonly SeedScope scope;
        private readonly List<(double time, Vector2 pos, Vector2 vel)> history = new();
        private Wander wander;
        private double lastTime;

        internal EarnedAccuracy Earned { get; }

        public AimPerception(AimSettings settings, SeedScope scope)
        {
            this.settings = settings;
            this.scope = scope;
            Earned = new EarnedAccuracy(settings, scope.Derive(EarnedStream));
            Reset();
        }

        public void Reset()
        {
            history.Clear();
            wander = new Wander(scope.Derive(WanderStream));
            lastTime = double.NaN;
            Earned.Reset();
        }

        public void Perceive(in Kinematics self, in EnemyTarget sighted, double now, out Vector2 pos, out Vector2 vel)
        {
            var dt = double.IsNaN(lastTime) ? 0f : (float)(now - lastTime);
            lastTime = now;

            history.Add((now, sighted.kinematics.pos, sighted.kinematics.vel));
            var seenAt = now - settings.reactionDelay;
            while (history.Count > 1 && history[1].time <= seenAt) history.RemoveAt(0);
            (_, pos, vel) = history[0];

            wander.Step(dt, settings.wanderCorrelationTime);
            var wanderDeg = settings.wanderCorrelationTime > 0f ? settings.wanderSize * wander.Value : 0f;
            Rotate(self.pos, wanderDeg + Earned.ErrorDeg(in self, in sighted, now), ref pos, ref vel);
        }

        private static void Rotate(Vector2 center, float deg, ref Vector2 pos, ref Vector2 vel)
        {
            if (deg == 0f) return;
            var c = Mathf.Cos(deg * Mathf.Deg2Rad);
            var s = Mathf.Sin(deg * Mathf.Deg2Rad);
            var rel = pos - center;
            pos = center + new Vector2(rel.x * c - rel.y * s, rel.x * s + rel.y * c);
            vel = new Vector2(vel.x * c - vel.y * s, vel.x * s + vel.y * c);
        }
    }
}

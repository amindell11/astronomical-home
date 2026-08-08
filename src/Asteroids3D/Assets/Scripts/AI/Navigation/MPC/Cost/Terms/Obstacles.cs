using Unity.Mathematics;

namespace Movement.MPC
{
    // Solver-owned: admissibility and collision are never brain-optional, so no objective can
    // weaken them. Character-axis strength lives in MpcSettings, not in a decision.
    public static partial class Cost
    {
        /// <summary>The hard collision penalty (fixed, un-ramped) and the gated turn-away cost (ramped) are mutually exclusive per step: an overlapping hull pays only the penalty, a clear hull only turn-away.</summary>
        internal static void ObstacleCosts(State s, in CostInput input, in Config cfg,
            float profileScale, out float collision, out float obstacle)
        {
            collision = 0f;
            obstacle = 0f;
            if (input.obstacleCount <= 0 || (cfg.collisionPenalty <= 0f && cfg.wObstacle <= 0f)) return;

            var hullRadius = cfg.shipRadius * profileScale + cfg.collisionSafetyMargin;
            if (Collides(s.pos, input.obstacles, input.obstacleCount, hullRadius))
                collision = cfg.collisionPenalty;
            else if (cfg.wObstacle > 0f)
                obstacle = TurnAwayCost(s.pos, s.vel, input.obstacles, input.obstacleCount,
                    hullRadius, cfg.maxLatAccel) * cfg.wObstacle;
        }

        /// <summary>Bank profile: cos(strafe * maxBank) is the fraction of the ship's cross-section visible in-plane — banking rolls the collider, narrowing the hull. Drives obstacle clearance.</summary>
        internal static float BankProfileScale(float strafe, in Config cfg)
            => cfg.maxBankAngleRad > 0f ? math.cos(math.abs(strafe) * cfg.maxBankAngleRad) : 1f;

        /// <summary>Hard hull-overlap test between the (bank-narrowed, margin-inflated) ship disc and any obstacle disc. Near-binary by design: misses aren't penalized for proximity, so close-and-tight flying stays free (trade study §3.4).</summary>
        internal static bool Collides(float2 pos,
            Unity.Collections.NativeArray<ObstacleData> obstacles, int count, float hullRadius)
        {
            for (var i = 0; i < count; i++)
            {
                var obs = obstacles[i];
                var range = obs.radius + hullRadius;
                if (math.lengthsq(obs.position - pos) < range * range)
                    return true;
            }
            return false;
        }

        /// <summary>Collision-course-gated turn-away cost: only obstacles the velocity leads into and can't sidestep before impact cost anything (0 when the sidestep suffices, →1 as it falls short, C¹ at the boundary); worst obstacle wins. Chosen over the stopping-distance ratio after the A2 ablation (see Chase_Nav_Track_A_Implementation_Log.md).</summary>
        internal static float TurnAwayCost(float2 pos, float2 vel,
            Unity.Collections.NativeArray<ObstacleData> obstacles, int count,
            float hullRadius, float maxLatAccel)
        {
            var speed = math.length(vel);
            if (speed <= 1e-3f) return 0f;

            var velDir = vel / speed;
            var halfLatAccel = 0.5f * math.max(maxLatAccel, 1e-4f);
            var worst = 0f;

            for (var i = 0; i < count; i++)
            {
                var obs = obstacles[i];
                var toObs = obs.position - pos;
                var corridor = obs.radius + hullRadius;

                var along = math.dot(toObs, velDir);
                if (along <= 0f) continue;

                var perp = math.length(toObs - along * velDir);
                if (perp >= corridor) continue;

                var lateralClearanceNeeded = corridor - perp;
                var timeToObstaclePlane = along / speed;
                var maxSidestepBeforeImpact = halfLatAccel * timeToObstaclePlane * timeToObstaclePlane;
                var deficit = math.saturate(1f - maxSidestepBeforeImpact / math.max(lateralClearanceNeeded, 1e-4f));
                worst = math.max(worst, deficit * deficit);
            }
            return worst;
        }
    }
}

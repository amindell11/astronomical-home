using UnityEngine;

namespace ShipControl.AI
{
    /// <summary>
    /// Orbit state – the ship circles an enemy at a fixed radius while maintaining offensive pressure.
    /// This logic was previously called KiteState but is now renamed for clarity.
    /// </summary>
    public class OrbitState : AIState
    {
        // Configuration (identical to previous KiteState)
        private readonly float orbitRadius = 15f;
        private readonly float minOrbitRadius = 10f;
        private readonly float maxOrbitRadius = 25f;
        private readonly float orbitLeadTime = 2f;

        // State
        private bool orbitClockwise = true;
        private float stateEntryTime;

        public OrbitState(AINavigator navigator, AIGunner gunner) : base(navigator, gunner) { }

        public override void Enter(AIContext ctx)
        {
            base.Enter(ctx);
            stateEntryTime = Time.time;

            if (ctx?.Enemy != null)
            {
                Vector2 relativeVel = ctx.EnemyRelVelocity;
                Vector2 toEnemy = ctx.VectorToEnemy;
                float cross = relativeVel.x * toEnemy.y - relativeVel.y * toEnemy.x;
                orbitClockwise = Mathf.Abs(cross) < 1f ? Random.value > 0.5f : cross > 0f;
            }
        }

        public override void Tick(AIContext ctx, float deltaTime)
        {
            if (ctx?.Enemy == null) return;

            // Predict intercept point for aiming
            Vector2 predicted = gunner.PredictIntercept(
                ctx.SelfPosition,
                ctx.SelfVelocity,
                ctx.EnemyPos,
                ctx.EnemyVel,
                ctx.LaserSpeed);

            gunner.SetTarget(predicted);
            navigator.SetFacingTarget(predicted - ctx.SelfPosition);

            // Compute dynamic orbit waypoint
            Vector2 orbitPoint = navigator.ComputeOrbitPoint(
                ctx.EnemyPos,
                ctx.SelfPosition,
                ctx.SelfVelocity,
                orbitClockwise,
                orbitRadius,
                orbitLeadTime);

            navigator.SetNavigationPoint(orbitPoint, avoid: true);

            // Occasionally flip orbit direction for unpredictability
            if (Time.time - stateEntryTime > 3f && Random.value < 0.1f * deltaTime)
            {
                orbitClockwise = !orbitClockwise;
            }
        }

        public override void Exit()
        {
            base.Exit();
            navigator.ClearNavigationPoint();
            navigator.ClearFacingOverride();
        }

        public override float ComputeUtility(AIContext ctx)
        {
            if (ctx?.Enemy == null) return 0f;

            float score = 0.4f;
            float healthFactor = (ctx.HealthPct + ctx.ShieldPct) / 2f;
            if (healthFactor > 0.3f && healthFactor < 0.9f) score += 0.2f;

            float dist = ctx.VectorToEnemy.magnitude;
            if (dist >= minOrbitRadius && dist <= maxOrbitRadius) score += 0.3f;
            else if (dist > maxOrbitRadius * 1.5f) score -= 0.2f;

            if (ctx.LineOfSightToEnemy) score += 0.2f;
            score += AIUtilityCurves.FearCurve(ctx.LaserHeatPct, 0.1f);
            score += AIUtilityCurves.DesireCurve(ctx.MissileAmmo, 0.1f);

            int netThreat = ctx.NearbyEnemyCount - ctx.NearbyFriendCount;
            if (netThreat > 2) score -= 0.3f;
            if (healthFactor < 0.25f) score -= 0.4f;
            return Mathf.Max(0f, score);
        }
    }
} 
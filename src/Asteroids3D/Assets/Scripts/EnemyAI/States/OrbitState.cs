using UnityEngine;

namespace EnemyAI.States
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

            // Start with a baseline attack utility since orbit is an offensive maneuver
            float score = AIUtility.ComputeAttackUtility(ctx);
            
            // Strong bonus for being in the optimal orbit sweet spot
            float dist = ctx.VectorToEnemy.magnitude;
            if (dist >= minOrbitRadius && dist <= maxOrbitRadius)
            {
                score += 0.4f;
            }
            
            // Bonus for not having a direct line of sight - good for flanking
            if (!ctx.LineOfSightToEnemy)
            {
                score += 0.3f;
            }

            // Orbit is less desirable at very low health; Attack or Evade are better.
            float healthFactor = (ctx.HealthPct + ctx.ShieldPct) / 2f;
            if (healthFactor < 0.25f)
            {
                score -= 0.4f;
            }
            
            return Mathf.Max(0f, score);
        }
    }
} 
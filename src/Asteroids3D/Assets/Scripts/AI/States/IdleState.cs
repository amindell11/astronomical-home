using UnityEngine;

namespace ShipControl.AI
{
    /// <summary>
    /// Idle state - ship remains stationary and takes no actions.
    /// High utility when health/shield is low and no immediate threats.
    /// </summary>
    public class IdleState : AIState
    {
        public IdleState(AINavigator navigator, AIGunner gunner) : base(navigator, gunner)
        {
        }

        public override void Enter(AIContext ctx)
        {
            base.Enter(ctx);
            
            // Clear any existing navigation target
            navigator.ClearNavigationPoint();
            gunner.SetTarget(null);
        }

        public override void Tick(AIContext ctx, float deltaTime)
        {
            // In idle state, we do nothing - no movement, no shooting
            // The ship will naturally come to a stop due to drag
        }

        public override void Exit()
        {
            base.Exit();
        }

        public override float ComputeUtility(AIContext ctx)
        {
            float score = 0.1f; // Base minimal score
            
            // Increase if no enemies nearby
            if (ctx.NearbyEnemyCount == 0)
                score += 0.3f;
                
            // Increased desire to be idle at low health/shield (defensive posture).
            // This is a "fear" response, so we use the weakest link (min health/shield).
            float healthFactor = Mathf.Min(ctx.HealthPct, ctx.ShieldPct);
            score += AIUtilityCurves.FearCurve(healthFactor, 0.3f); // Max bonus of 0.3 when health/shield is zero.
                
            return score;
        }
        
        public override void OnDrawGizmos(AIContext ctx)
        {
            base.OnDrawGizmos(ctx);
            
            #if UNITY_EDITOR
            if (ctx?.SelfTransform == null) return;
            
            Vector3 position = ctx.SelfTransform.position;
            
            // Draw idle indicator - a pulsing circle
            Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.3f + 0.2f * Mathf.Sin(Time.time * 2f));
            Gizmos.DrawWireSphere(position, 2f);
            #endif
        }
    }
} 
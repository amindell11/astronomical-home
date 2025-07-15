using UnityEngine;

namespace ShipControl.AI
{
    /// <summary>
    /// Kite state - ship maintains orbital movement around enemy at optimal firing range.
    /// High utility when health is moderate, enemy exists, and we can maintain standoff distance.
    /// </summary>
    public class KiteState : AIState
    {
        // Configuration
        [Header("Kite Parameters")] 
        private readonly float kiteRadius = 15f;
        private readonly float minKiteRadius = 10f;
        private readonly float maxKiteRadius = 25f;
        private readonly float orbitLeadTime = 2f;
        
        // State
        private bool orbitClockwise = true;
        private float stateEntryTime;
        
        public KiteState(AINavigator navigator, AIGunner gunner) : base(navigator, gunner)
        {
        }

        public override void Enter(AIContext ctx)
        {
            base.Enter(ctx);
            
            stateEntryTime = Time.time;
            
            // Choose initial orbit direction based on relative velocity or random
            if (ctx.Enemy != null)
            {
                Vector2 relativeVel = ctx.EnemyRelVelocity;
                Vector2 toEnemy = ctx.VectorToEnemy;
                
                // Use cross product to determine if we should orbit clockwise or counter-clockwise
                // If relative velocity has a component perpendicular to the enemy vector, continue in that direction
                float crossProduct = relativeVel.x * toEnemy.y - relativeVel.y * toEnemy.x;
                orbitClockwise = crossProduct > 0f;
                
                // If no significant relative velocity, choose randomly
                if (Mathf.Abs(crossProduct) < 1f)
                {
                    orbitClockwise = Random.value > 0.5f;
                }
                
                RLog.AI($"[KiteState] Entering with orbit direction: {(orbitClockwise ? "clockwise" : "counter-clockwise")}");
            }
        }

        public override void Tick(AIContext ctx, float deltaTime)
        {
            if (ctx.Enemy == null) return;
            
            // Calculate predicted intercept point for targeting
            Vector2 predictedTarget = gunner.PredictIntercept(
                ctx.SelfPosition,
                ctx.SelfVelocity,
                ctx.EnemyPos,
                ctx.EnemyVel,
                ctx.LaserSpeed
            );
            
            // Set gunner target to predicted intercept point
            gunner.SetTarget(predictedTarget);
            
            // Calculate vector to predicted target for facing
            Vector2 vectorToPredictedTarget = predictedTarget - ctx.SelfPosition;
            
            // Compute orbit waypoint each tick to track moving enemy
            Vector2 orbitPoint = navigator.ComputeOrbitPoint(
                center: ctx.EnemyPos,
                selfPos: ctx.SelfPosition,
                selfVel: ctx.SelfVelocity,
                clockwise: orbitClockwise,
                radius: kiteRadius,
                leadTime: orbitLeadTime
            );
            
            // Set navigation target with avoidance
            navigator.SetNavigationPoint(orbitPoint, avoid: true);
            
            // Face the predicted target position while kiting
            navigator.SetFacingTarget(vectorToPredictedTarget);
            
            // Periodically consider reversing orbit direction to be unpredictable
            float timeInState = Time.time - stateEntryTime;
            if (timeInState > 3f && Random.value < 0.1f * deltaTime) // 10% chance per second after 3s
            {
                orbitClockwise = !orbitClockwise;
                RLog.AI($"[KiteState] Reversing orbit direction to {(orbitClockwise ? "clockwise" : "counter-clockwise")}");
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
            // Must have an enemy to kite
            if (ctx.Enemy == null)
                return 0f;
            
            float score = 0.4f; // Base moderate priority
            
            // Prefer kiting when health/shields are moderate (not too low, not perfect)
            float healthFactor = (ctx.HealthPct + ctx.ShieldPct) / 2f;
            if (healthFactor > 0.3f && healthFactor < 0.9f)
                score += 0.2f;
            
            // Bonus for being in optimal kiting range
            float distToEnemy = ctx.VectorToEnemy.magnitude;
            if (distToEnemy >= minKiteRadius && distToEnemy <= maxKiteRadius)
                score += 0.3f;
            else if (distToEnemy > maxKiteRadius * 1.5f)
                score -= 0.2f; // Too far for effective kiting
            
            // Bonus for having line of sight (can actually fire while kiting)
            if (ctx.LineOfSightToEnemy)
                score += 0.2f;
            
            // Bonus for having weapons available
            score += AIUtilityCurves.FearCurve(ctx.LaserHeatPct, 0.1f);
            score += AIUtilityCurves.DesireCurve(ctx.MissileAmmo, 0.1f);
            
            // Penalty if severely outnumbered (should evade instead)
            int netThreat = ctx.NearbyEnemyCount - ctx.NearbyFriendCount;
            if (netThreat > 2)
                score -= 0.3f;
            
            // Bonus if we're faster than the enemy (better kiting effectiveness)
            if (ctx.SpeedPct > 0.5f)
                score += 0.1f;
            
            // Penalty if health is critically low (should evade)
            if (healthFactor < 0.25f)
                score -= 0.4f;
                
            return Mathf.Max(0f, score);
        }
        
        public override void OnDrawGizmos(AIContext ctx)
        {
            base.OnDrawGizmos(ctx);
            
            #if UNITY_EDITOR
            if (ctx?.SelfTransform == null) return;
            
            Vector3 selfPos = ctx.SelfTransform.position;
            
            // Draw kite radius circle
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.3f); // Cyan
            if (ctx.Enemy != null)
            {
                Vector3 enemyPos = GamePlane.PlaneToWorld(ctx.EnemyPos);
                Gizmos.DrawWireSphere(enemyPos, kiteRadius);
                
                // Draw min/max kite range
                Gizmos.color = new Color(1f, 1f, 0f, 0.2f); // Yellow
                Gizmos.DrawWireSphere(enemyPos, minKiteRadius);
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f); // Orange  
                Gizmos.DrawWireSphere(enemyPos, maxKiteRadius);
                
                // Draw orbit direction indicator
                Vector2 toEnemy = ctx.VectorToEnemy.normalized;
                Vector2 tangent = orbitClockwise ? 
                    new Vector2(toEnemy.y, -toEnemy.x) : 
                    new Vector2(-toEnemy.y, toEnemy.x);
                
                Vector3 tangent3D = GamePlane.PlaneVectorToWorld(tangent);
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(selfPos, tangent3D * 5f);
                
                // Draw arrowhead for orbit direction
                Vector3 perpLeft = Vector3.Cross(tangent3D, Vector3.forward).normalized;
                Vector3 arrowTip = selfPos + tangent3D * 5f;
                Gizmos.DrawLine(arrowTip, arrowTip - tangent3D * 1f + perpLeft * 0.5f);
                Gizmos.DrawLine(arrowTip, arrowTip - tangent3D * 1f - perpLeft * 0.5f);
                
                // Line to enemy
                float distToEnemy = ctx.VectorToEnemy.magnitude;
                Gizmos.color = ctx.LineOfSightToEnemy ? Color.green : Color.red;
                Gizmos.DrawLine(selfPos, enemyPos);
                
                // Show kite state info
                UnityEditor.Handles.color = Color.white;
                string info = $"KITE ({(orbitClockwise ? "CW" : "CCW")})\n";
                info += $"Range: {distToEnemy:F1}m (target: {kiteRadius:F0}m)\n";
                info += $"HP: {ctx.HealthPct:P0} Shield: {ctx.ShieldPct:P0}\n";
                if (ctx.LineOfSightToEnemy) info += "✓ Clear shot";
                else info += "✗ No LOS";
                
                UnityEditor.Handles.Label(selfPos + Vector3.up * 4f, info);
            }
            #endif
        }
    }
} 
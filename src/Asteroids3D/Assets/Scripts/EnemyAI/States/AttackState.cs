using Editor;
using Game;
using UnityEngine;

namespace EnemyAI.States
{
    /// <summary>
    /// Attack state - ship actively engages enemy targets.
    /// High utility when health is good, weapons are ready, and target is accessible.
    /// </summary>
    public class AttackState : AIState
    {
        private Transform lastTarget;
        
        // Configuration
        private const float TargetUpdateInterval = 0.5f; // How often to update navigation target
        private float lastTargetUpdate;
        
        private const float DefaultFacingDistance = 6f;
        private const float DefaultFacingSpeed = -1f;
        
        public AttackState(AINavigator navigator, AIGunner gunner) : base(navigator, gunner)
        {
        }

        public override void Enter(AIContext context)
        {
            RLog.AI($"[AttackState] Entering. Target: {context.Enemy?.name ?? "None"}");
        }

        public override void Tick(AIContext context, float deltaTime)
        {
            // Continuously update nav point to track the enemy
            if (context.Enemy == null) return;   
            
            // Calculate predicted intercept point for targeting
            Vector2 predictedTarget = gunner.PredictIntercept(
                context.SelfPosition,
                context.SelfVelocity,
                context.EnemyPos,
                context.EnemyVel,
                context.LaserSpeed
            );
            
            // Set gunner target to predicted intercept point
            gunner.SetTarget(predictedTarget);
            
            // Calculate vector to predicted target for facing
            Vector2 vectorToPredictedTarget = predictedTarget - context.SelfPosition;
            
            if(context.VectorToEnemy.magnitude < DefaultFacingDistance || Vector3.Dot(context.EnemyRelVelocity, context.VectorToEnemy) < DefaultFacingSpeed){ // if enemy is close or we're closing fast enough, face it
                navigator.SetFacingTarget(vectorToPredictedTarget);
            }
            else
            {
                navigator.ClearFacingOverride();
            }
            navigator.SetNavigationPoint(
                context.EnemyPos,
                true, // enable avoidance
                context.EnemyVel);

        }

        public override void Exit()
        {
            RLog.AI("[AttackState] Exiting");
            navigator.ClearNavigationPoint();
            navigator.ClearFacingOverride();
        }

        public override float ComputeUtility(AIContext ctx)
        {
            if (ctx.Enemy == null)
                return 0f;

            // Start with the general-purpose attack utility
            float score = AIUtility.ComputeAttackUtility(ctx);

            // Add a large, specific bonus for attacking a weakened enemy to encourage finishing them off.
            // This makes the AttackState more "bloodthirsty" than other offensive states.
            float enemyHealthFactor = (ctx.EnemyHealthPct + ctx.EnemyShieldPct) / 2f;
            score += AIUtility.FearCurve(enemyHealthFactor, 0.3f);

            float dist = ctx.VectorToEnemy.magnitude;
            // Bonus for being far away, outside of typical orbit range
            if (dist > 25f) // OrbitState's maxOrbitRadius is 25f
            {
                score += 0.2f;
            }

            // Per user instructions, an attack can become a favorable option
            // if health is low, representing a desperate all-in maneuver
            // as opposed to a prolonged engagement like Orbit.
            score += AIUtility.FearCurve(ctx.HealthPct, 0.15f);

            return score;
        }
        
        public override void OnDrawGizmos(AIContext ctx)
        {
            base.OnDrawGizmos(ctx);
            
            #if UNITY_EDITOR
            if (ctx?.SelfTransform == null) return;
            
            Vector3 position = ctx.SelfTransform.position;
                    
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            Gizmos.DrawWireSphere(position, DefaultFacingDistance); // Close range
            
            // Draw target information if we have an enemy
            if (ctx.Enemy != null)
            {
                Vector3 enemyPos = GamePlane.PlaneToWorld(ctx.EnemyPos);
                float distToEnemy = ctx.VectorToEnemy.magnitude;
                
                // Line to enemy - color based on line of sight
                Gizmos.color = ctx.LineOfSightToEnemy ? Color.red : new Color(1f, 0.5f, 0f, 0.7f);
                Gizmos.DrawLine(position, enemyPos);
                
                // Enemy marker
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(enemyPos, Vector3.one * 2f);
                
                // Show targeting crosshair if close
                if (distToEnemy < DefaultFacingDistance)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(enemyPos, 1f);
                    
                    // Draw crosshair
                    Vector3 cross = Vector3.one * 0.5f;
                    Gizmos.DrawLine(enemyPos - cross, enemyPos + cross);
                    cross.x *= -1;
                    Gizmos.DrawLine(enemyPos - cross, enemyPos + cross);
                }
                
                // Enemy info label
                UnityEditor.Handles.color = Color.red;
                string enemyInfo = $"TARGET: {ctx.Enemy.name}\n{distToEnemy:F1}m";
                if (ctx.LineOfSightToEnemy) enemyInfo += " (LOS)";
                else enemyInfo += " (No LOS)";
                enemyInfo += $"\nEnemy HP: {ctx.EnemyHealthPct:P0}";
                UnityEditor.Handles.Label(enemyPos + Vector3.up, enemyInfo);
            }
            
            // Show attack state info
            UnityEditor.Handles.color = Color.white;
            string info = $"ATTACK\nHP: {ctx.HealthPct:P0} Shield: {ctx.ShieldPct:P0}";
            info += $"\nLaser Heat: {ctx.LaserHeatPct:P0}";
            info += $"\nMissiles: {ctx.MissileAmmo}";
            if (ctx.NearbyEnemyCount > ctx.NearbyFriendCount)
                info += $"\n⚠ Outnumbered {ctx.NearbyEnemyCount}v{ctx.NearbyFriendCount}";
            UnityEditor.Handles.Label(position + Vector3.up * 4f, info);
            #endif
        }
    }
} 
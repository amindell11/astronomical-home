using Game;
using UnityEngine;

namespace EnemyAI.States
{
    /// <summary>
    /// Kite state – the ship maintains distance by retreating along a vector opposite to the enemy's movement while still facing and attacking.
    /// </summary>
    public class KiteState : AIState
    {
        private const float DesiredKiteDistance = 10f;
        private const float MinKiteDistance = 5f;
        private const float MaxKiteDistance = 25f;
        
        public KiteState(AINavigator navigator, AIGunner gunner) : base(navigator, gunner) { }

        public override void Enter(AIContext ctx)
        {
            base.Enter(ctx);
        }

        public override void Tick(AIContext ctx, float deltaTime)
        {
            if (ctx.Enemy == null) return;

            // Predict intercept for aiming
            Vector2 predictedTarget = gunner.PredictIntercept(
                ctx.SelfPosition,
                ctx.SelfVelocity,
                ctx.EnemyPos,
                ctx.EnemyVel,
                ctx.LaserSpeed);
            
            // Face the enemy (predicted point)
            gunner.SetTarget(predictedTarget);
            navigator.SetFacingTarget(predictedTarget - ctx.SelfPosition);

            // Compute retreat direction (away from enemy and opposite their velocity)
            Vector2 dirAway = (ctx.SelfPosition - ctx.EnemyPos).normalized;
            Vector2 dirOppVel = ctx.EnemyVel.sqrMagnitude > 0.01f ? (-ctx.EnemyVel).normalized : Vector2.zero;
            Vector2 retreatDir = (dirAway + dirOppVel).normalized;
            if (retreatDir.sqrMagnitude < 0.01f) retreatDir = dirAway; // fallback

            // Base waypoint
            Vector2 waypoint = ctx.SelfPosition + retreatDir * DesiredKiteDistance;

            float distToEnemy = ctx.VectorToEnemy.magnitude;
            if (distToEnemy < MinKiteDistance)
            {
                // Too close – push further away
                waypoint = ctx.SelfPosition + retreatDir * (MinKiteDistance + 5f);
            }
            else if (distToEnemy > MaxKiteDistance)
            {
                // Too far – move back toward enemy slightly to stay engaged
                waypoint = ctx.EnemyPos + (-retreatDir) * DesiredKiteDistance * 0.5f;
            }

            navigator.SetNavigationPoint(waypoint, avoid: true);
        }

        public override void Exit()
        {
            base.Exit();
            navigator.ClearNavigationPoint();
            navigator.ClearFacingOverride();
        }

        public override float ComputeUtility(AIContext ctx)
        {
            if (ctx.Enemy == null) return 0f;
            
            // Kite is a mix of attack and evade. Start with the average of the two.
            float attackDesire = AIUtility.ComputeAttackUtility(ctx);
            float evadeDesire = AIUtility.ComputeEvadeUtility(ctx);
            float score = (attackDesire + evadeDesire) / 2f;
            
            // Bonus for being too close, making kiting a priority to regain distance.
            float dist = ctx.VectorToEnemy.magnitude;
            if (dist < MinKiteDistance)
            {
                score += 0.3f;
            }
            
            // Kiting is ideal when we need to evade but have strong weapons.
            // This is a "fighting retreat."
            bool hasGoodWeapons = ctx.MissileAmmo > 0 && ctx.LaserHeatPct < 0.5f;
            if (evadeDesire > 0.5f && hasGoodWeapons)
            {
                score += 0.25f;
            }

            // Kiting is also a good option if we have low health but good shields
            // allowing us to absorb some hits while creating distance.
            if (ctx.HealthPct < 0.4f && ctx.ShieldPct > 0.6f)
            {
                score += 0.2f;
            }

            // Penalty if not facing the enemy while trying to kite.
            // A small tolerance (e.g., 30 degrees) is allowed before applying a penalty.
            float anglePenalty = Mathf.Clamp01((ctx.SelfAngleToEnemy - 30f) / 150f);
            score -= anglePenalty * 0.3f;

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
                Vector3 enemyPos = GamePlane.PlanePointToWorld(ctx.EnemyPos);
                Gizmos.DrawWireSphere(enemyPos, DesiredKiteDistance);
                
                // Draw min/max kite range
                Gizmos.color = new Color(1f, 1f, 0f, 0.2f); // Yellow
                Gizmos.DrawWireSphere(enemyPos, MinKiteDistance);
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f); // Orange  
                Gizmos.DrawWireSphere(enemyPos, MaxKiteDistance);
                
                // Draw arrowhead for retreat direction
                Vector2 dirAway = (ctx.SelfPosition - ctx.EnemyPos).normalized;
                Vector2 dirOppVel = ctx.EnemyVel.sqrMagnitude > 0.01f ? (-ctx.EnemyVel).normalized : Vector2.zero;
                Vector2 retreatDir = (dirAway + dirOppVel).normalized;
                if (retreatDir.sqrMagnitude < 0.01f) retreatDir = dirAway; // fallback

                Vector3 retreatDir3D = GamePlane.PlaneDirToWorld(retreatDir);
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(selfPos, retreatDir3D * 5f);
                
                // Draw arrowhead for orbit direction
                Vector3 perpLeft = Vector3.Cross(retreatDir3D, Vector3.forward).normalized;
                Vector3 arrowTip = selfPos + retreatDir3D * 5f;
                Gizmos.DrawLine(arrowTip, arrowTip - retreatDir3D * 1f + perpLeft * 0.5f);
                Gizmos.DrawLine(arrowTip, arrowTip - retreatDir3D * 1f - perpLeft * 0.5f);
                
                // Line to enemy
                float distToEnemy = ctx.VectorToEnemy.magnitude;
                Gizmos.color = ctx.LineOfSightToEnemy ? Color.green : Color.red;
                Gizmos.DrawLine(selfPos, enemyPos);
                
                // Show kite state info
                UnityEditor.Handles.color = Color.white;
                string info = $"KITE (Retreat)\n";
                info += $"Range: {distToEnemy:F1}m (target: {DesiredKiteDistance:F0}m)\n";
                info += $"HP: {ctx.HealthPct:P0} Shield: {ctx.ShieldPct:P0}\n";
                if (ctx.LineOfSightToEnemy) info += "✓ Clear shot";
                else info += "✗ No LOS";
                
                UnityEditor.Handles.Label(selfPos + Vector3.up * 4f, info);
            }
            #endif
        }
    }
} 
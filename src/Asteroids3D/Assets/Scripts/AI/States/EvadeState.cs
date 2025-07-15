using UnityEngine;

namespace ShipControl.AI
{
    /// <summary>
    /// Evade state - ship retreats from threats.
    /// High utility when health/shield is low, enemies are nearby, or missiles are incoming.
    /// </summary>
    public class EvadeState : AIState
    {
        private Vector2 evadePoint;
        
        // Configuration
        private const float DefaultFleeDistance = 30f;

        public EvadeState(AINavigator navigator, AIGunner gunner) : base(navigator, gunner)
        {
        }

        public override void Enter(AIContext ctx)
        {
            base.Enter(ctx);
            
            gunner.SetTarget(null);
        }

        public override void Tick(AIContext ctx, float deltaTime)
        {
            evadePoint = CalculateEvadePoint(ctx);
            navigator.SetNavigationPoint(evadePoint, true);
        }

        public override void Exit()
        {
            base.Exit();
            navigator.ClearNavigationPoint();
        }

        public override float ComputeUtility(AIContext ctx)
        {
            // Base score: always a baseline desire to evade if threatened
            float score = 0.3f;

            // Strong incentive to evade if shields/health are low, using "fear" curves.
            score += AIUtilityCurves.FearCurve(ctx.HealthPct, 0.4f);
            score += AIUtilityCurves.FearCurve(ctx.ShieldPct, 0.3f);

            // Increase score if outnumbered
            if (ctx.NearbyEnemyCount > ctx.NearbyFriendCount + 1)
                score += 0.2f;

            // Increase score if an enemy has a clear shot
            if (ctx.Enemy != null && ctx.LineOfSightToEnemy)
                score += 0.2f;

            // Adjust score based on closing speed: positive when enemy is closing on us, negative when opening distance
            if (ctx.Enemy != null)
            {
                // Scale contribution to a reasonable range [-0.2, +0.2]
                float closingContribution = Mathf.Clamp(ctx.ClosingSpeed * 0.02f, -0.2f, 0.2f);
                score += closingContribution;
            }

            // Adjust score based on enemy facing: more likely to evade if enemy is pointing at us
            if (ctx.Enemy != null)
            {
                float facingFactor = Mathf.Cos(ctx.EnemyAngleToSelf * Mathf.Deg2Rad); // 1 when enemy faces us, -1 when away
                float facingContribution = facingFactor * 0.2f; // Map to [-0.2, +0.2]
                score += facingContribution;
            }

            // Increase score as laser heat increases, using a curve
            score += AIUtilityCurves.DesireCurve(ctx.LaserHeatPct, 0.1f);
            score += AIUtilityCurves.DesireCurve(ctx.EnemyMissileAmmo, 0.1f);

            // Major bonus if incoming missile detected
            if (ctx.IncomingMissile)
                score += 0.5f;
                
            // Decrease if very close to target (might need to fight through)
            if (ctx.Enemy != null)
            {
                float distToEnemy = ctx.VectorToEnemy.magnitude;
                if (distToEnemy < 7f && ctx.LineOfSightToEnemy)
                    score -= 0.2f;
            }
                
            return Mathf.Max(0f, score);
        }

        private Vector2 CalculateEvadePoint(AIContext ctx)
        {
            Vector2 selfPos = ctx.SelfPosition;
            Vector2 fleeDirection = Vector2.zero;
            
            // Primary threat: the current enemy
            if (ctx.Enemy != null && ctx.Enemy.gameObject.activeInHierarchy)
            {
                fleeDirection = -ctx.VectorToEnemy.normalized; // Flee AWAY from enemy
            }else{
                fleeDirection = Random.insideUnitCircle.normalized;
            }
            
            // Calculate the evade point in 2D planespace
            return selfPos + fleeDirection * DefaultFleeDistance;
        }

        public override void OnDrawGizmos(AIContext ctx)
        {
            base.OnDrawGizmos(ctx);
            
            #if UNITY_EDITOR
            if (ctx?.SelfTransform == null) return;
            
            Vector3 selfPos = ctx.SelfTransform.position;

            Vector3 evadePos3D = GamePlane.PlaneToWorld(evadePoint);
                
            Gizmos.color = Color.green;
            Gizmos.DrawLine(selfPos, evadePos3D);
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(evadePos3D, 1f);
            
            // Draw distance to evade point
            float distToEvadePoint = Vector2.Distance(ctx.SelfPosition, evadePoint);
            UnityEditor.Handles.color = Color.cyan;
            UnityEditor.Handles.Label(evadePos3D + Vector3.up, $"Evade Point\n{distToEvadePoint:F1}m");

            // Draw flee distance circle
            Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(selfPos, DefaultFleeDistance);

            // Draw threat indicators
            if (ctx.Enemy != null)
            {
                Vector3 enemyPos = new Vector3(ctx.EnemyPos.x, ctx.EnemyPos.y, selfPos.z);
                
                // Draw line to primary threat
                Gizmos.color = ctx.LineOfSightToEnemy ? Color.red : new Color(1f, 0.5f, 0f);
                Gizmos.DrawLine(selfPos, enemyPos);
                
                // Draw flee direction arrow
                Vector2 fleeDir = ctx.Enemy.gameObject.activeInHierarchy ? 
                    -ctx.VectorToEnemy.normalized : Random.insideUnitCircle.normalized;
                Vector3 fleeDir3D = new Vector3(fleeDir.x, fleeDir.y, 0);
                
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(selfPos, fleeDir3D * 5f);
                
                // Draw arrowhead for flee direction
                Vector3 perpLeft = Vector3.Cross(fleeDir3D, Vector3.forward).normalized;
                Vector3 arrowTip = selfPos + fleeDir3D * 5f;
                Gizmos.DrawLine(arrowTip, arrowTip - fleeDir3D * 1f + perpLeft * 0.5f);
                Gizmos.DrawLine(arrowTip, arrowTip - fleeDir3D * 1f - perpLeft * 0.5f);
            }

            // Highlight if incoming missile
            if (ctx.IncomingMissile)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(selfPos, Vector3.one * 3f);
            }

            // Draw state info
            UnityEditor.Handles.color = Color.white;
            string threatInfo = $"EVADE\nHP: {ctx.HealthPct:P0} Shield: {ctx.ShieldPct:P0}";
            if (ctx.IncomingMissile) threatInfo += "\n⚠ MISSILE!";
            if (ctx.NearbyEnemyCount > ctx.NearbyFriendCount) threatInfo += $"\n⚠ Outnumbered {ctx.NearbyEnemyCount}v{ctx.NearbyFriendCount}";
            
            UnityEditor.Handles.Label(selfPos + Vector3.up * 4f, threatInfo);
            #endif
        }
    }
} 
using Game;
using UnityEngine;

namespace EnemyAI.States
{
    /// <summary>
    /// Patrol state - ship moves to random waypoints when no enemies are detected.
    /// High utility when no valid enemy exists.
    /// </summary>
    public class PatrolState : AIState
    {
        private Vector2 currentTarget;
        private bool hasTarget = false;
        
        // Configuration
        private readonly float patrolRadius = 50f;
        private readonly float arriveThreshold = 5f;
        private readonly bool enableAvoidance = true;

        public PatrolState(AINavigator navigator, AIGunner gunner) : base(navigator, gunner)
        {
        }

        public override void Enter(AIContext ctx)
        {
            base.Enter(ctx);
            
            gunner.SetTarget(null);
            
            // Choose a new patrol point when entering the state
            ChooseNewPatrolPoint(ctx);
        }

        public override void Tick(AIContext context, float deltaTime)
        {
            // If we have reached the waypoint, find a new one
            if (!navigator.CurrentWaypoint.isValid || context.VectorToWaypoint.magnitude < navigator.arriveRadius)
            {
                ChooseNewPatrolPoint(context);
            }
        }

        public override void Exit()
        {
            base.Exit();
            hasTarget = false;
        }

        public override float ComputeUtility(AIContext ctx)
        {
            // If no enemy exists or enemy is inactive, patrol utility should be high
            if (!ctx.InCombat)
            {
                return 1f;
            }
            
            // If enemy exists and is active, patrol utility should be low
            return 0f;
        }

        private void ChooseNewPatrolPoint(AIContext ctx)
        {
            // Pick a random point within patrolRadius around current position
            Vector2 currentPos = ctx.SelfPosition;
            float randomDistance = Random.Range(patrolRadius * 0.3f, patrolRadius);
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            currentTarget = currentPos + randomDirection * randomDistance;

            hasTarget = true;

            // Set the navigation target using plane coordinates
            navigator.SetNavigationPoint(currentTarget, enableAvoidance);
        }
        
        public override void OnDrawGizmos(AIContext ctx)
        {
            base.OnDrawGizmos(ctx);
            
            #if UNITY_EDITOR
            if (ctx?.SelfTransform == null) return;
            
            Vector3 position = ctx.SelfTransform.position;
            
            // Draw patrol radius
            Gizmos.color = new Color(0f, 1f, 0f, 0.1f);
            Gizmos.DrawWireSphere(position, patrolRadius);
            
            // Draw current patrol target if we have one
            if (hasTarget)
            {
                // Convert plane coordinates to world coordinates for rendering
                Vector3 currentTargetWorld = GamePlane.PlaneToWorld(currentTarget);
                
                // Line to target
                Gizmos.color = Color.green;
                Gizmos.DrawLine(position, currentTargetWorld);
                
                // Target marker
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(currentTargetWorld, 1.5f);
                Gizmos.DrawWireCube(currentTargetWorld, Vector3.one * 0.5f);
                
                float distToTarget = Vector2.Distance(ctx.SelfPosition, currentTarget);
                UnityEditor.Handles.color = Color.green;
                UnityEditor.Handles.Label(currentTargetWorld + Vector3.up, $"Patrol Target\n{distToTarget:F1}m");
            }
            
            // Show patrol state info
            UnityEditor.Handles.color = Color.white;
            string info = $"PATROL\nRadius: {patrolRadius:F0}m";
            if (hasTarget)
                info += "\nMoving to waypoint";
            else
                info += "\nSearching for waypoint";
            UnityEditor.Handles.Label(position + Vector3.up * 4f, info);
            #endif
        }
    }
} 
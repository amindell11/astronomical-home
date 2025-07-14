using UnityEngine;

namespace ShipControl.AI
{
    /// <summary>
    /// Patrol state - ship moves to random waypoints when no enemies are detected.
    /// High utility when no valid enemy exists.
    /// </summary>
    public class PatrolState : AIState
    {
        private Vector3 currentTarget;
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
            bool enemyExists = ctx.Enemy != null;
            bool enemyActiveInHierarchy = enemyExists && ctx.Enemy.gameObject.activeInHierarchy;
            
            // If no enemy exists or enemy is inactive, patrol utility should be high
            if (!enemyExists || !enemyActiveInHierarchy)
            {
                return 1f;
            }
            
            // If enemy exists and is active, patrol utility should be low
            return 0f;
        }

        private void ChooseNewPatrolPoint(AIContext ctx)
        {
            if (navigator == null) return;
            
            // Try to pick a point that is visible on the player's screen so the AI stays within view
            Camera cam = Camera.main;

            if (cam != null)
            {
                // Pick a random viewport coordinate with 10% padding so we do not hug the edges
                const float pad = 0.1f;
                Vector3 viewport = new Vector3(
                    Random.Range(pad, 1f - pad),
                    Random.Range(pad, 1f - pad),
                    0f);

                // Re-use ship depth so projection lands roughly in the game plane
                Vector3 shipScreen = cam.WorldToScreenPoint(ctx.SelfPosition3D);
                viewport.z = shipScreen.z;

                // Convert to world space and project onto the GamePlane to ensure we stay flat
                Vector3 worldPoint = cam.ViewportToWorldPoint(viewport);
                Vector3 planePoint = GamePlane.Origin + GamePlane.ProjectOntoPlane(worldPoint);

                currentTarget = planePoint;
            }
            else
            {
                // Fallback: pick a random point within patrolRadius around current position
                Vector3 currentPos = ctx.SelfPosition3D;
                float randomDistance = Random.Range(patrolRadius * 0.3f, patrolRadius);
                Vector3 randomOffset = GamePlane.ProjectOntoPlane(Random.insideUnitSphere).normalized * randomDistance;
                currentTarget = currentPos + randomOffset;
            }

            hasTarget = true;

            // Set the navigation target
            navigator.SetNavigationPointWorld(currentTarget, enableAvoidance);
        }
    }
} 
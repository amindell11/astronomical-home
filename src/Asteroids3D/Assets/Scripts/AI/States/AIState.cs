using UnityEngine;

namespace ShipControl.AI
{
    /// <summary>
    /// Abstract base class for AI states in the finite state machine.
    /// Each state encapsulates its own behavior logic and utility calculation.
    /// </summary>
    public abstract class AIState
    {
        protected readonly AINavigator navigator;
        protected readonly AIGunner gunner;
        protected readonly string StateName;

        protected AIState(AINavigator navigator, AIGunner gunner)
        {
            this.navigator = navigator;
            this.gunner = gunner;
            StateName = GetType().Name;
        }

        /// <summary>
        /// Called when the state becomes active
        /// </summary>
        public virtual void Enter(AIContext ctx)
        {
            RLog.AI($"[{StateName}] Enter");
        }

        /// <summary>
        /// Called every FixedUpdate while active
        /// </summary>
        public abstract void Tick(AIContext ctx, float deltaTime);

        /// <summary>
        /// Called before transitioning away
        /// </summary>
        public virtual void Exit()
        {
            RLog.AI($"[{StateName}] Exit");
        }

        /// <summary>
        /// Returns utility score [0,1] given current context.
        /// Higher values indicate this state is more desirable.
        /// </summary>
        public abstract float ComputeUtility(AIContext ctx);

        #region Convenience Helpers
        /// <summary>
        /// Sets navigation target directly on the navigator
        /// </summary>
        protected void SetNavigationTarget(Vector2 planePos, bool avoid = true, Vector2? velocity = null)
        {
            navigator?.SetNavigationPoint(planePos, avoid, velocity);
        }

        /// <summary>
        /// Sets navigation target from a world-space position, handling conversion to plane coordinates.
        /// </summary>
        protected void SetNavigationTarget(Vector3 worldPos, bool avoid = true, Vector3? velocity = null)
        {
            Vector2 planePos = GamePlane.WorldToPlane(worldPos);
            Vector2? planeVel = velocity.HasValue ? GamePlane.WorldToPlane(velocity.Value) : (Vector2?)null;
            navigator?.SetNavigationPoint(planePos, avoid, planeVel);
        }

        /// <summary>
        /// Clears the navigation waypoint
        /// </summary>
        protected void ClearNavigationTarget()
        {
            navigator?.ClearNavigationPoint();
        }

        /// <summary>
        /// Sets the gunner's target transform.
        /// </summary>
        protected void SetGunnerTarget(Transform target)
        {
            if (gunner != null)
            {
                gunner.Target = target;
            }
        }
        #endregion
    }
} 
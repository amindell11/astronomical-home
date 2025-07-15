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

        /// <summary>
        /// Draw debug gizmos for this state. Override in derived classes for state-specific visualization.
        /// </summary>
        public virtual void OnDrawGizmos(AIContext ctx)
        {
        }
    }
} 
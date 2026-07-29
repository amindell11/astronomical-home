using UnityEngine;

namespace AI
{
    /// <summary>One decision's commanded output, as handed to <see cref="IIntentChooser.Decide"/>'s intent.</summary>
    public readonly struct PolicyAction
    {
        public readonly Vector2 worldVelocity;
        public readonly float facingRad;
        public readonly float facingWeight;

        public PolicyAction(Vector2 worldVelocity, float facingRad, float facingWeight)
        {
            this.worldVelocity = worldVelocity;
            this.facingRad = facingRad;
            this.facingWeight = facingWeight;
        }
    }

    /// <summary>Read-only access to a chooser's recent commanded actions, for debug gizmos to draw
    /// without coupling the render side to the decision policy's own state.</summary>
    public interface IPolicyReadout
    {
        /// <summary>Actions currently held, 0..capacity.</summary>
        int Count { get; }

        /// <summary>The i-th most recent action; 0 = newest, Count-1 = oldest.</summary>
        PolicyAction ActionFromNewest(int index);
    }
}

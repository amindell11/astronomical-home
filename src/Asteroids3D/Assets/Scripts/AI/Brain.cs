using System;
using AI.Context;
using UnityEngine;

namespace AI
{
    /// <summary>Policy-agnostic decision host for a swappable <see cref="IIntentChooser"/>.</summary>
    [DefaultExecutionOrder(-70)]
    public class Brain : MonoBehaviour
    {
        [Tooltip("The decision policy (e.g. an InferenceChooser running a trained checkpoint). Authored on the prefab or installed at runtime.")]
        [SerializeReference] private IIntentChooser chooser;

        public IIntentChooser Chooser => chooser;

        /// <summary>Harness/test seam for swapping the decision policy.</summary>
        internal void InstallChooser(IIntentChooser next)
        {
            chooser = next;
        }

        public BrainDecision? Decide(AIContext ctx, float dt)
        {
            if (chooser == null)
                throw new InvalidOperationException($"Brain on '{name}' has no chooser authored or installed.");
            return chooser.Decide(ctx, dt);
        }

        /// <summary>Resets the policy's accumulated decision state (respawn reset).</summary>
        public void ResetState() => chooser?.Reset();
    }
}

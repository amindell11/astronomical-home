using AI.Context;
using AI.States;
using AI.Utility;
using UnityEngine;

namespace AI
{
    /// <summary>
    /// The ship's decision host: a policy-agnostic component that composes a swappable
    /// <see cref="IIntentChooser"/> (picked in the inspector) and exposes its per-tick
    /// decision to <see cref="AICommander"/>. Swapping the policy — utility now, RL later —
    /// is a change to the serialized <see cref="chooser"/> reference; the host, the commander,
    /// and actuation are untouched.
    /// </summary>
    [DefaultExecutionOrder(-70)]
    public partial class Brain : MonoBehaviour
    {
        [Tooltip("The decision policy. Defaults to UtilityChooser; swap for another IIntentChooser (e.g. an RL policy).")]
        [SerializeReference] private IIntentChooser chooser = new UtilityChooser();

        public IIntentChooser Chooser => chooser;

        // Populates the default policy when the component is added or reset, so the
        // SerializeReference is never empty and needs no manual type-picking.
        private void Reset() => chooser ??= new UtilityChooser();

        public NavigationIntent Decide(AIContext ctx, float dt) =>
            chooser?.Decide(ctx, dt) ?? NavigationIntent.None;
    }
}

using AI.Context;
using AI.States;
using AI.Utility;
using Movement.MPC;
using UnityEngine;

namespace AI
{
    /// <summary>
    /// The ship's decision host: a policy-agnostic component that composes a swappable
    /// <see cref="IIntentChooser"/> (picked in the inspector) and exposes its per-tick
    /// decision to <see cref="AICommander"/>. Swapping the policy — utility now, RL later —
    /// is a change to the serialized <see cref="chooser"/> reference; the host, the commander,
    /// and actuation are untouched.
    ///
    /// Brain owns the discrete behavior menu (<see cref="stateProfiles"/>) and builds it into the
    /// policy. The commander only knows it has a Brain to ask for an intent — it never sees the
    /// states themselves.
    /// </summary>
    [DefaultExecutionOrder(-70)]
    public partial class Brain : MonoBehaviour
    {
        [Tooltip("The decision policy. Defaults to UtilityChooser; swap for another IIntentChooser (e.g. an RL policy).")]
        [SerializeReference] private IIntentChooser chooser = new UtilityChooser();

        [Header("State Profiles")]
        [Tooltip("Discrete behaviors available to a state-based policy. A continuous policy ignores these.")]
        [SerializeField] private StateProfile[] stateProfiles;

        public IIntentChooser Chooser => chooser;

        private Navigator navigator;
        private Gunner gunner;
        private int seed;

        /// <summary>
        /// Binds the actuators the discrete behaviors drive and builds the option set into the
        /// policy. A continuous policy (raw <see cref="IIntentChooser"/>) ignores the profiles.
        /// </summary>
        public void Initialize(Navigator navigator, Gunner gunner, int seed)
        {
            this.navigator = navigator;
            this.gunner = gunner;
            this.seed = seed;
            BuildStates();
        }

        private void BuildStates()
        {
            if (chooser is not IStateChooser stateChooser || stateProfiles == null || stateProfiles.Length == 0)
                return;

            var states = new AIState[stateProfiles.Length];
            for (var i = 0; i < stateProfiles.Length; i++)
            {
                if (!stateProfiles[i]) return;
                states[i] = new AIState(stateProfiles[i], navigator, gunner, seed);
            }
            stateChooser.Initialize(states, seed);
        }

        public NavigationIntent Decide(AIContext ctx, float dt) =>
            chooser?.Decide(ctx, dt) ?? NavigationIntent.None;
    }
}

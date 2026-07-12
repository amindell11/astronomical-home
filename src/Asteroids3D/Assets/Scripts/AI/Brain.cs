using AI.Context;
using AI.States;
using AI.Utility;
using Movement.MPC;
using Ships.Command;
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
    public class Brain : MonoBehaviour
    {
        [Tooltip("The decision policy. Defaults to UtilityChooser; swap for another IIntentChooser (e.g. an RL policy).")]
        [SerializeReference] private IIntentChooser chooser = new UtilityChooser();

        [Header("State Profiles")]
        [Tooltip("Discrete behaviors available to a state-based policy. A continuous policy ignores these.")]
        [SerializeField] private StateProfile[] stateProfiles;

        public IIntentChooser Chooser => chooser;

        private const uint UtilitySamplerStream = 1;
        private const uint GoalStream = 2;

        private Navigator navigator;
        private Gunner gunner;
        private SeedScope strategyScope;

        /// <summary>
        /// Binds the actuators the discrete behaviors drive and builds the option set into the
        /// policy. A continuous policy (raw <see cref="IIntentChooser"/>) ignores the profiles.
        /// </summary>
        public void Initialize(Navigator navigator, Gunner gunner, SeedScope strategyScope)
        {
            this.navigator = navigator;
            this.gunner = gunner;
            this.strategyScope = strategyScope;
            BuildStates();
        }

        private void BuildStates()
        {
            if (chooser is not IStateChooser stateChooser || stateProfiles == null || stateProfiles.Length == 0)
                return;

            var goalScope = strategyScope.Derive(GoalStream);
            var states = new AIState[stateProfiles.Length];
            for (var i = 0; i < stateProfiles.Length; i++)
            {
                if (!stateProfiles[i]) return;
                states[i] = new AIState(stateProfiles[i], navigator, gunner, goalScope);
            }
            stateChooser.Initialize(states, strategyScope.Derive(UtilitySamplerStream));
        }

        public NavigationIntent Decide(AIContext ctx, float dt) =>
            chooser?.Decide(ctx, dt) ?? NavigationIntent.None;

#if UNITY_EDITOR
        // Hot-reloads the policy when its config changes during play: this component's
        // stateProfiles array here, or a profile asset's contents via StateProfile.OnValidate.
        private void OnValidate() => RefreshStates();

        /// <summary>Editor-only: rebuild the state set from the current profiles and restart state
        /// selection. No-op before the brain is initialized (i.e. outside play).</summary>
        internal void RefreshStates()
        {
            if (navigator == null) return;
            (chooser as UtilityChooser)?.ResetForTesting();
            BuildStates();
        }
#endif
    }
}

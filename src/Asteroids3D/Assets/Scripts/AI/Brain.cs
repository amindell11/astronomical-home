using AI.Context;
using AI.States;
using AI.Utility;
using Movement.MPC;
using Ships.Command;
using UnityEngine;

namespace AI
{
    /// <summary>Policy-agnostic decision host: composes a swappable <see cref="IIntentChooser"/> and owns the discrete behavior menu (<see cref="stateProfiles"/>) it builds into state-based policies.</summary>
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

        /// <summary>Binds the actuators the discrete behaviors drive and builds the option set into the policy; a continuous policy ignores the profiles.</summary>
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

        /// <summary>Harness/test seam: swap the decision policy; rebuilds the option set when already initialized.</summary>
        internal void InstallChooser(IIntentChooser next)
        {
            chooser = next;
            if (navigator != null) BuildStates();
        }

        public NavigationIntent Decide(AIContext ctx, float dt) =>
            chooser?.Decide(ctx, dt) ?? NavigationIntent.None;

        /// <summary>Restores the freshly-initialized policy: the chooser clears its state and rebuilds from the stored strategy scope, so its RNG streams replay from the spawn seed.</summary>
        public void ResetState()
        {
            chooser?.Reset();
            BuildStates();
        }

#if UNITY_EDITOR
        private void OnValidate() => RefreshStates();

        /// <summary>Editor-only: rebuild the state set from the current profiles. No-op before the brain is initialized (i.e. outside play).</summary>
        internal void RefreshStates()
        {
            if (navigator == null) return;
            ResetState();
        }
#endif
    }
}

using AI.Context;
using AI.States;
using AI.Utility;
using Movement.MPC;
using Ships.Command;
using UnityEngine;

namespace AI
{
    /// <summary>Policy-agnostic decision host: binds the actuators and seed to a swappable <see cref="IIntentChooser"/>.</summary>
    [DefaultExecutionOrder(-70)]
    public class Brain : MonoBehaviour
    {
        [Tooltip("The decision policy. Defaults to UtilityChooser; swap for another IIntentChooser (e.g. an RL policy).")]
        [SerializeReference] private IIntentChooser chooser = new UtilityChooser();

        public IIntentChooser Chooser => chooser;

        private Navigator navigator;
        private Gunner gunner;
        private SeedScope strategyScope;

        public void Initialize(Navigator navigator, Gunner gunner, SeedScope strategyScope)
        {
            this.navigator = navigator;
            this.gunner = gunner;
            this.strategyScope = strategyScope;
            InitializeChooser();
        }

        private void InitializeChooser()
        {
            if (chooser is IStateChooser stateChooser)
                stateChooser.Initialize(navigator, gunner, strategyScope);
        }

        /// <summary>Harness/test seam for swapping the decision policy.</summary>
        internal void InstallChooser(IIntentChooser next)
        {
            chooser = next;
            if (navigator != null) InitializeChooser();
        }

        public NavigationIntent Decide(AIContext ctx, float dt) =>
            chooser?.Decide(ctx, dt) ?? NavigationIntent.None;

        /// <summary>Rebuilds the policy from the stored scope so its RNG streams replay from the spawn seed.</summary>
        public void ResetState()
        {
            chooser?.Reset();
            InitializeChooser();
        }

#if UNITY_EDITOR
        private void OnValidate() => RefreshStates();

        internal void RefreshStates()
        {
            if (navigator == null) return;
            ResetState();
        }
#endif
    }
}

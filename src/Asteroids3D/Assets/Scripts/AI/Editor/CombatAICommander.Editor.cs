#if UNITY_EDITOR
using AI.States;
using AI.Utility;

namespace AI
{
    public partial class CombatAICommander
    {
        private void OnValidate()
        {
            RefreshStates();
        }

        internal void RefreshStates()
        {
            if (!systemsInitialized || stateProfiles == null || stateProfiles.Length == 0)
                return;

            var states = new State[stateProfiles.Length];
            for (var i = 0; i < stateProfiles.Length; i++)
            {
                if (stateProfiles[i] == null) return;
                states[i] = new AIState(stateProfiles[i], Navigator, gunner, utilityTuning);
            }

            utilitySelector.ResetForTesting();
            utilitySelector.Initialize(utilityTuning, states);
        }
    }
}
#endif

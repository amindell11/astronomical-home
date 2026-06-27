#if UNITY_EDITOR
using AI.Debug;
using AI.States;
using UnityEngine;

namespace AI
{
    public partial class AICommander
    {
        [Header("Debug")]
        [SerializeField] private AIDebugSettings debugSettings;
        public AIDebugSettings DebugSettings => debugSettings;

        // Waypoint visualization lives in Navigator.Editor, gated on AIDebugChannel.Steering

        private void OnValidate()
        {
            RefreshStates();
        }

        internal void RefreshStates()
        {
            if (!systemsInitialized || stateProfiles == null || stateProfiles.Length == 0)
                return;

            var uc = UtilityChooser;
            if (uc == null) return;

            var states = new AIState[stateProfiles.Length];
            for (var i = 0; i < stateProfiles.Length; i++)
            {
                if (stateProfiles[i] == null) return;
                states[i] = new AIState(stateProfiles[i], Navigator, Gunner);
            }

            uc.ResetForTesting();
            uc.Initialize(states);
        }
    }
}
#endif

#if UNITY_EDITOR
using AI.Debug;
using UnityEngine;

namespace AI
{
    public partial class AICommander
    {
        [Header("Debug")]
        [SerializeField] private AIDebugSettings debugSettings;
        public AIDebugSettings DebugSettings => debugSettings;

        // Waypoint visualization lives in Navigator.Editor, gated on AIDebugChannel.Steering.
        // State (re)building lives in Brain; profile edits refresh via Brain.RefreshStates.
    }
}
#endif

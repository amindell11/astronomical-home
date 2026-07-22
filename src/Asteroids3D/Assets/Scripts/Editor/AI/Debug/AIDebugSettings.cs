using System;
using UnityEngine;

namespace AI.Debug
{
    [Flags]
    public enum AIDebugChannel
    {
        None       = 0,
        Targeting  = 1 << 0,
        Steering   = 1 << 2,
        Scanning   = 1 << 3,
        Info       = 1 << 5,
        Observation = 1 << 7,
    }

    [CreateAssetMenu(fileName = "AIDebugSettings", menuName = "AI/Debug Settings")]
    public class AIDebugSettings : ScriptableObject
    {
        public AIDebugChannel activeChannels = AIDebugChannel.None;

        [Tooltip("Draw gizmos for all ships, not just the selected one")]
        public bool alwaysDrawGizmos;

        public bool IsActive(AIDebugChannel ch) => (activeChannels & ch) != 0;
        public bool ShouldDraw(bool isSelected) => isSelected || alwaysDrawGizmos;
    }
}

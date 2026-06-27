using System;
using AI.States;
using UnityEngine;

namespace AI.Utility
{
    [Serializable]
    public struct StateWeight
    {
        public StateProfile profile;
        [Range(0f, 2f)] public float weight;

        public StateWeight(StateProfile profile, float weight = 1f)
        {
            this.profile = profile;
            this.weight = weight;
        }
    }

    /// <summary>
    /// Reusable state weight configuration. Can be shared or per-instance.
    /// Maps StateProfile assets to weight multipliers.
    /// </summary>
    [CreateAssetMenu(fileName = "UtilityWeights", menuName = "AI/Utility Weights")]
    public class UtilityWeights : ScriptableObject
    {
        [SerializeField] private StateWeight[] weights;

        public float this[string profileName]
        {
            get
            {
                if (weights == null) return 1f;
                foreach (var w in weights)
                    if (w.profile && w.profile.name == profileName) return w.weight;
                return 1f;
            }
        }
    }
}

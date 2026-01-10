using System;
using AI.States;
using UnityEngine;

namespace AI.Utility
{
    [Serializable]
    public struct StateWeight
    {
        [HideInInspector] public StateType type;
        [Range(0f, 2f)] public float weight;

        public StateWeight(StateType type, float weight = 1f)
        {
            this.type = type;
            this.weight = weight;
        }
    }
    
    /// <summary>
    /// Reusable state weight configuration. Can be shared or per-instance.
    /// </summary>
    [CreateAssetMenu(fileName = "UtilityWeights", menuName = "AI/Utility Weights")]
    public class UtilityWeights : ScriptableObject
    {
        [SerializeField] private StateWeight[] weights;

        public float this[StateType type]
        {
            get
            {
                EnsureInitialized();
                foreach (var w in weights)
                    if (w.type == type) return w.weight;
                return 1f;
            }
        }

        private void OnEnable() => EnsureInitialized();

        public void EnsureInitialized()
        {
            var types = (StateType[])Enum.GetValues(typeof(StateType));
            if (weights != null && weights.Length == types.Length) return;
            
            weights = new StateWeight[types.Length];
            for (var i = 0; i < types.Length; i++)
                weights[i] = new StateWeight(types[i], 1f);
        }
    }
}

using AI;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Serialized catalog of the prefabs the episode composition instantiates, so the harness spawns from typed references (baked into the training player build) instead of editor-only AssetDatabase path loads. One asset at <see cref="AssetPath"/> is the single source of truth every host — training scene, tests, eval — shares.</summary>
    [CreateAssetMenu(fileName = "HarnessAssets", menuName = "RL/Harness Assets")]
    public sealed class HarnessAssets : ScriptableObject
    {
        public const string AssetPath = "Assets/Settings/RL/HarnessAssets.asset";

        [SerializeField] private Ship shipPrefab;
        [SerializeField] private AICommander agentPilot;
        [SerializeField] private AICommander baselinePilot;
        [SerializeField] private GameObject fieldPrefab;

        public Ship ShipPrefab => shipPrefab;
        public AICommander AgentPilot => agentPilot;
        public AICommander BaselinePilot => baselinePilot;
        public GameObject FieldPrefab => fieldPrefab;
    }
}

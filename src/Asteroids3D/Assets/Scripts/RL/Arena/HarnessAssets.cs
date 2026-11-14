using AI;
using Ships;
using UnityEngine;

namespace RL.Arena
{
    /// <summary>Serialized catalog of the prefabs the matrix lane's weapon-pair compositions instantiate, so the harness spawns from typed references. One asset at <see cref="AssetPath"/> is the single source of truth the harness host and the tests share.</summary>
    [CreateAssetMenu(fileName = "HarnessAssets", menuName = "RL/Harness Assets")]
    public sealed class HarnessAssets : ScriptableObject
    {
        public const string AssetPath = "Assets/Settings/RL/HarnessAssets.asset";

        [SerializeField] private Ship shipPrefab;
        [SerializeField] private AICommander agentPilot;

        public Ship ShipPrefab => shipPrefab;
        public AICommander AgentPilot => agentPilot;
    }
}

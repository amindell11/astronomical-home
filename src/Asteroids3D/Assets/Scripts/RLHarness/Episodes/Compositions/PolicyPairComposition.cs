using Game.Services;
using Unity.InferenceEngine;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace Game.RLHarness
{
    internal sealed class PolicyPairComposition : ISessionComposition
    {
        public EpisodeLoopDriver Driver { get; }
        public EpisodePair Pair { get; }

        private readonly ShipAgent agentA;
        private readonly ShipAgent agentB;

        public PolicyPairComposition(UnitService units, Vector2 offset, IProjectileService projectiles,
            HarnessAssets assets, in RewardSpec spec, ModelAsset candidateModel, ModelAsset opponentModel,
            HarnessField field)
        {
            Pair = EpisodePair.SpawnSelfPlayPair(units, offset, field?.Field, projectiles, in spec, assets,
                out var brainA, out var brainB);
            (agentA, agentB) = ShipAgentFactory.ComposeSelfPlayPair(Pair, brainA, brainB, in spec, offset,
                BehaviorType.InferenceOnly, parent: null, candidateModel, opponentModel);
            Driver = new EpisodeLoopDriver(Pair, agentA, offset, field, roster: null, opponentAgent: agentB);
        }

        public OpponentDraw InstallOpponent(in OpponentSpec opponent, in RewardSpec spec, int episodeIndex,
            Vector2 arenaCenter) =>
            new() { archetype = opponent.Label };

        public void Dispose()
        {
            if (agentA) Object.DestroyImmediate(agentA.gameObject);
            if (agentB) Object.DestroyImmediate(agentB.gameObject);
            Pair.Dispose();
        }
    }
}

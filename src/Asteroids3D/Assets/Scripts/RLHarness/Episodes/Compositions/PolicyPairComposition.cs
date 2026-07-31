using Game.Services;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Checkpoint-vs-checkpoint composition: both ships driven by frozen checkpoints InferenceOnly. The mirror is the same-path degenerate — one shared ModelRunner, exactly the parameter-shared pair. There is nothing to install per episode, so the draw exists purely to fingerprint the JSONL row (<c>Mirror</c>, or the opponent checkpoint's stem).</summary>
    internal sealed class PolicyPairComposition : ISessionComposition
    {
        public EpisodeLoopDriver Driver { get; }
        public EpisodePair Pair { get; }

        private readonly ShipAgent agentA;
        private readonly ShipAgent agentB;

        public PolicyPairComposition(UnitService units, ArenaContext arena, IProjectileService projectiles,
            HarnessAssets assets, in RewardSpec spec, string candidateOnnxPath, string opponentOnnxPath,
            HarnessField field)
        {
            Pair = EpisodePair.SpawnSelfPlayPair(units, arena, projectiles, in spec, assets,
                out var chooserA, out var chooserB);
            (agentA, agentB) = ShipAgentFactory.ComposeSelfPlayPair(Pair, chooserA, chooserB, in spec, arena.Offset,
                BehaviorType.InferenceOnly, parent: null, candidateOnnxPath, opponentOnnxPath);
            Driver = new EpisodeLoopDriver(Pair, agentA, arena.Offset, field, roster: null, opponentAgent: agentB);
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

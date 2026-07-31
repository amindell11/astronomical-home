using Game.Services;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Checkpoint-vs-itself composition: both ships parameter-shared on one frozen checkpoint InferenceOnly. There is nothing to install per episode, so the draw it returns exists purely to fingerprint the JSONL row as a mirror match.</summary>
    internal sealed class MirrorComposition : ISessionComposition
    {
        public EpisodeLoopDriver Driver { get; }
        public EpisodePair Pair { get; }

        private readonly ShipAgent agentA;
        private readonly ShipAgent agentB;

        public MirrorComposition(UnitService units, ArenaContext arena, IProjectileService projectiles,
            HarnessAssets assets, in RewardSpec spec, string onnxAssetPath, HarnessField field)
        {
            Pair = EpisodePair.SpawnSelfPlayPair(units, arena, projectiles, in spec, assets,
                out var chooserA, out var chooserB);
            (agentA, agentB) = ShipAgentFactory.ComposeSelfPlayPair(Pair, chooserA, chooserB, in spec, arena.Offset,
                BehaviorType.InferenceOnly, parent: null, onnxAssetPath);
            Driver = new EpisodeLoopDriver(Pair, agentA, arena.Offset, field, roster: null, opponentAgent: agentB);
        }

        public OpponentDraw InstallOpponent(in OpponentSpec opponent, in RewardSpec spec, int episodeIndex,
            Vector2 arenaCenter) =>
            new() { archetype = OpponentSpec.MirrorLabel };

        public void Dispose()
        {
            if (agentA) Object.DestroyImmediate(agentA.gameObject);
            if (agentB) Object.DestroyImmediate(agentB.gameObject);
            Pair.Dispose();
        }
    }
}

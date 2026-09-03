using Game.Services;
using Unity.InferenceEngine;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Checkpoint-vs-scripted-roster composition: the canonical pair driven by a pinned checkpoint InferenceOnly, against an <see cref="OpponentRoster"/> the caller installs per episode. The arena, projectile service and asteroid field are the host's — unlike the training compositions, one eval session composes them once and only the pair per seed.</summary>
    internal sealed class InferenceRosterComposition : ISessionComposition
    {
        public EpisodeLoopDriver Driver { get; }
        public EpisodePair Pair { get; }

        private readonly OpponentRoster roster;
        private readonly ShipAgent agent;

        public InferenceRosterComposition(UnitService units, WorldHandle world, IProjectileService projectiles,
            HarnessAssets assets, in RewardSpec spec, ModelAsset model, HarnessField field)
        {
            Pair = EpisodePair.SpawnWithAgentBrain(units, world, projectiles, in spec, assets, out var brain);
            roster = new OpponentRoster(Pair.Baseline, Pair.Agent);
            agent = ShipAgentFactory.ComposeInferenceOnly(Pair, brain, in spec, world.Offset, model);
            Driver = new EpisodeLoopDriver(Pair, agent, world.Offset, field);
        }

        public OpponentDraw InstallOpponent(in OpponentSpec opponent, in RewardSpec spec, int episodeIndex,
            Vector2 arenaCenter) =>
            roster.Install(opponent.archetype, in spec, episodeIndex, arenaCenter);

        public void Dispose()
        {
            roster.Dispose();
            if (agent) Object.DestroyImmediate(agent.gameObject);
            Pair.Dispose();
        }
    }
}

using System;
using Game.Services;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The scripted-roster training composition: arena + optional asteroid field + agent-vs-scripted-baseline pair + opponent roster + ShipAgent, wired into an EpisodeLoopDriver. Built once from the initial spec; the host owns the per-episode loop and env-param overlay. PR-4b's self-play pair is the sibling composition the generic host will select between.</summary>
    internal sealed class ScriptedRosterComposition : IEpisodeComposition
    {
        public EpisodeLoopDriver Driver { get; }
        private readonly OpponentRoster roster;

        public ScriptedRosterComposition(GameObject host, in RewardSpec spec, BehaviorType behaviorType, HarnessAssets assets, Vector2 offset)
        {
            var units = host.AddComponent<UnitService>();
            var (arena, projectiles) = ShipServices.Compose(units, host.transform, offset, presentationEnabled: false);
            var field = spec.useAsteroidField
                ? HarnessField.Spawn(arena, assets, spec.fieldDensityScale, host.transform, presentationEnabled: false)
                : null;
            var pair = EpisodePair.SpawnWithAgentBrain(units, arena, projectiles, in spec, assets, out var brain);
            roster = new OpponentRoster(pair.Baseline, pair.Agent);

            var agent = behaviorType switch
            {
                BehaviorType.Default => ShipAgentFactory.ComposeForTraining(pair, brain, in spec, arena.Offset, host.transform),
                BehaviorType.HeuristicOnly => ShipAgentFactory.ComposeHeuristicOnly(pair, brain, in spec, arena.Offset, host.transform),
                _ => throw new NotSupportedException(
                    $"Training supports Default (trainer) and HeuristicOnly; {behaviorType} checkpoint eval runs through CheckpointEvaluator."),
            };

            Driver = new EpisodeLoopDriver(pair, agent, arena.Offset, field, roster);
        }

        public void Dispose() => roster?.Dispose();
    }
}

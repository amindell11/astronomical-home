using Game.Services;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The symmetric self-play composition: arena + optional asteroid field + a two-agent EpisodePair where both ships are parameter-shared ShipCombat agents (team 0 and team 1), wired into an EpisodeLoopDriver that steps both. Team 0 is the primary (logged); the team-1 ghost trains off the mirror runner. The sibling of ScriptedRosterComposition the generic host selects between.</summary>
    internal sealed class SelfPlayComposition : IEpisodeComposition
    {
        public EpisodeLoopDriver Driver { get; }

        public SelfPlayComposition(GameObject host, in RewardSpec spec, BehaviorType behaviorType, HarnessAssets assets, Vector2 offset)
        {
            var units = host.AddComponent<UnitService>();
            var projectiles = ShipServices.Compose(units, host.transform, presentationEnabled: false);
            var field = spec.useAsteroidField
                ? HarnessField.Spawn(offset, assets, spec.fieldDensityScale, host.transform, presentationEnabled: false)
                : null;
            var pair = EpisodePair.SpawnSelfPlayPair(units, offset, field?.Field, projectiles, in spec, assets, out var brainA, out var brainB);
            var (agentA, agentB) = ShipAgentFactory.ComposeSelfPlayPair(
                pair, brainA, brainB, in spec, offset, behaviorType, host.transform);
            Driver = new EpisodeLoopDriver(pair, agentA, offset, field, roster: null, opponentAgent: agentB);
        }

        public void Dispose() { }
    }
}

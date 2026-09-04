using Game.Services;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The Stage A3 sentence-session composition: the measured ship on the agent slot plays one row's fixed sentence through an installed <see cref="SentenceBrain"/> (the controller probe reads that slot's solver), against the row's scripted opponent installed per episode through the roster primitive. Both ships are scripted, so the driver paces with a null agent and episodes end by rule.</summary>
    internal sealed class SentenceComposition : ISessionComposition
    {
        public EpisodeLoopDriver Driver { get; }
        public EpisodePair Pair { get; }

        private readonly OpponentRoster roster;

        public SentenceComposition(UnitService units, WorldHandle world, IProjectileService projectiles,
            HarnessAssets assets, in RewardSpec spec, HarnessField field, SentenceRow row)
        {
            Pair = EpisodePair.Spawn(units, world, projectiles, in spec, (commander, baselineShip) =>
            {
                var brain = commander.InstallBrain<SentenceBrain>();
                brain.Configure(baselineShip, row);
                return brain;
            }, assets);
            roster = new OpponentRoster(Pair.Baseline, Pair.Agent);
            Driver = new EpisodeLoopDriver(Pair, agent: null, world.Offset, field);
        }

        public OpponentDraw InstallOpponent(in OpponentSpec opponent, in RewardSpec spec, int episodeIndex,
            Vector2 arenaCenter)
        {
            var draw = roster.Install(opponent.archetype, in spec, episodeIndex, arenaCenter);
            // Rows must carry the row token, not the archetype — probe pools and JSONL reads key on it.
            draw.archetype = opponent.Label;
            return draw;
        }

        public void Dispose()
        {
            roster.Dispose();
            Pair.Dispose();
        }
    }
}

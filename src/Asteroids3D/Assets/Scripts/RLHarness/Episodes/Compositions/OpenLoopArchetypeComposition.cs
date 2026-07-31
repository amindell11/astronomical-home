using Game.Services;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The K1-2 velrebase measurement composition: the measured ship is the archetype-under-test on the baseline slot (installed per block through the roster primitive, legacy or anchored drive, wVelTrack clone included), the enemy on the agent slot is the non-reactive <see cref="FixedCircuitChooser"/>. No ShipAgent exists — both ships are scripted, so the driver paces episodes with a null agent and every episode ends by rule, not reward.</summary>
    internal sealed class OpenLoopArchetypeComposition : ISessionComposition
    {
        public EpisodeLoopDriver Driver { get; }
        public EpisodePair Pair { get; }

        private readonly OpponentRoster roster;

        public OpenLoopArchetypeComposition(UnitService units, ArenaContext arena, IProjectileService projectiles,
            HarnessAssets assets, in RewardSpec spec, HarnessField field)
        {
            Pair = EpisodePair.Spawn(units, arena, projectiles, in spec, (agentShip, baselineShip) =>
            {
                var mover = new FixedCircuitChooser();
                mover.Configure(baselineShip, arena.Offset);
                return mover;
            }, assets);
            roster = new OpponentRoster(Pair.Baseline, Pair.Agent);
            Driver = new EpisodeLoopDriver(Pair, agent: null, arena.Offset, field);
        }

        public OpponentDraw InstallOpponent(in OpponentSpec opponent, in RewardSpec spec, int episodeIndex,
            Vector2 arenaCenter)
        {
            var draw = roster.Install(opponent.archetype, in spec, episodeIndex, arenaCenter, opponent.drive);
            // Rows must carry the arm, not just the archetype — the paired delta reads off this label.
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

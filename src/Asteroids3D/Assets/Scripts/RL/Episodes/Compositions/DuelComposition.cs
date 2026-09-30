using AI;
using Combat.Weapons;
using UnityEngine;
using Substrate.Services.Units;
using Substrate.Services.Projectiles;
using RL.Arena;
using RL.Hosts;
using RL.Opponents;
using RL.Reward;

namespace RL.Episodes.Compositions
{
    /// <summary>The duel lane's harness composition: the shooter on the agent slot carries one weapon alone in its primary weapon slot and flies a fixed Aggressor bound to the target; the unarmed target on the baseline slot takes each block's opponent archetype per episode through the roster primitive. Both ships are scripted, so the driver paces with a null agent and episodes end by rule.</summary>
    internal sealed class DuelComposition : IHarnessComposition
    {
        // ArchetypePilot.prefab's authored shape; 10 u is the Missiles dumbfire boundary.
        private static readonly OpponentDraw ShooterShape = new() { desiredRange = 10f, speedFraction = 0.85f };

        public EpisodeLoopDriver Driver { get; }
        public EpisodePair Pair { get; }

        private readonly OpponentRoster roster;
        private readonly Vector2 arenaCenter;

        public DuelComposition(UnitService units, Vector2 offset, IProjectileService projectiles,
            HarnessAssets assets, in RewardSpec spec, WeaponComponent weapon)
        {
            arenaCenter = offset;
            Pair = EpisodePair.SpawnArmedPair(units, offset, field: null, projectiles, in spec, assets, weapon);
            Pair.Agent.GetComponentInChildren<AICommander>().InstallBrain<ArchetypeBrain>()
                .Configure(Pair.Baseline, OpponentArchetype.Aggressor, in ShooterShape, jukeSeed: 0, offset,
                    spec.arenaRadius);
            roster = new OpponentRoster(Pair.Baseline, Pair.Agent);
            Driver = new EpisodeLoopDriver(Pair, agent: null, offset);
        }

        public OpponentDraw InstallOpponent(in OpponentSpec opponent, in RewardSpec spec, int episodeIndex) =>
            roster.Install(opponent.archetype, in spec, episodeIndex, arenaCenter);

        public void Dispose()
        {
            roster.Dispose();
            Pair.Dispose();
        }
    }
}

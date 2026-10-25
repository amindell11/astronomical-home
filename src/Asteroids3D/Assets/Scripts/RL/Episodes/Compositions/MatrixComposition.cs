using AI;
using Combat.Weapons;
using Ships;
using UnityEngine;
using Substrate.Services.Units;
using Substrate.Services.Projectiles;
using RL.Arena;
using RL.Episodes;
using RL.Hosts;
using RL.Hosts.Lanes;
using RL.Opponents;
using RL.Reward;

namespace RL.Episodes.Compositions
{
    /// <summary>The matrix lane's harness composition. Both ships carry one primary weapon and fly a fixed Aggressor at each other on TestPilotMPC. The loadout binds at spawn, so one seed's episodes share the pair and an odd episode only swaps poses.</summary>
    internal sealed class MatrixComposition : IHarnessComposition
    {
        // DuelComposition's fixed Aggressor: hold 10, speed fraction 0.85.
        private static readonly OpponentDraw ShooterShape = new() { desiredRange = 10f, speedFraction = 0.85f };

        public EpisodeLoopDriver Driver { get; }
        public EpisodePair Pair { get; }

        private readonly UnitService units;
        private readonly AICommander agentCommander;
        private readonly AICommander baselineCommander;
        private readonly Vector2 arenaCenter;
        private SpawnPoses poses;
        private bool swapPending;
        private bool applyingSwap;
        private int respawnCount;

        public MatrixComposition(UnitService units, Vector2 offset, IProjectileService projectiles,
            HarnessAssets assets, in RewardSpec spec, WeaponComponent weaponA, WeaponComponent weaponB)
        {
            this.units = units;
            arenaCenter = offset;
            Pair = MatrixSpawner.Spawn(units, offset, field: null, projectiles, in spec, assets, weaponA, weaponB);
            agentCommander = Pair.Agent.GetComponentInChildren<AICommander>();
            baselineCommander = Pair.Baseline.GetComponentInChildren<AICommander>();
            Driver = new EpisodeLoopDriver(Pair, agent: null, offset);
            units.OnShipRespawned += OnRespawned;
        }

        public OpponentDraw InstallOpponent(in OpponentSpec opponent, in RewardSpec spec, int episodeIndex)
        {
            var arenaRadius = spec.arenaRadius;
            agentCommander.InstallBrain<ArchetypeBrain>()
                .Configure(Pair.Baseline, OpponentArchetype.Aggressor, in ShooterShape, jukeSeed: 0,
                    arenaCenter, arenaRadius);
            baselineCommander.InstallBrain<ArchetypeBrain>()
                .Configure(Pair.Agent, OpponentArchetype.Aggressor, in ShooterShape, jukeSeed: 0,
                    arenaCenter, arenaRadius);
            poses = EpisodePoses.Derive(in spec, MatrixLane.PoseIndex(episodeIndex), arenaCenter);
            swapPending = MatrixLane.SidesSwapped(episodeIndex);
            respawnCount = 0;
            return new OpponentDraw
            {
                archetype = OpponentArchetype.Aggressor.ToString(),
                desiredRange = ShooterShape.desiredRange,
                speedFraction = ShooterShape.speedFraction,
            };
        }

        public void Dispose()
        {
            if (units) units.OnShipRespawned -= OnRespawned;
            Pair.Dispose();
        }

        // Odd episodes swap sides after Reset seats weapon A on the agent pose.
        private void OnRespawned(Ship _)
        {
            if (!swapPending || applyingSwap) return;
            if (++respawnCount < 2) return;
            swapPending = false;
            respawnCount = 0;
            applyingSwap = true;
            units.RespawnShip(Pair.Agent.Id, poses.baselinePos, poses.baselineRotDeg);
            units.RespawnShip(Pair.Baseline.Id, poses.agentPos, poses.agentRotDeg);
            applyingSwap = false;
        }
    }
}

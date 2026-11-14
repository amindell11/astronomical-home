using System;
using Ships;
using UnityEngine;
using Substrate.Services.Units;
using Substrate.Services.Projectiles;
using RL.Reward;

namespace RL.Episodes
{
    /// <summary>The 1v1 pair an episode runs on: agent and baseline ships, reset atomically to each episode's poses. <see cref="MatrixSpawner"/> builds it through the private constructor by reflection.</summary>
    public sealed class EpisodePair : IDisposable
    {
        public Ship Agent { get; }
        public Ship Baseline { get; }

        private readonly UnitService units;
        private readonly IProjectileService projectiles;
        private readonly Vector2 arenaCenter;

        private EpisodePair(UnitService units, IProjectileService projectiles, Vector2 arenaCenter,
            Ship agent, Ship baseline)
        {
            this.units = units;
            this.projectiles = projectiles;
            this.arenaCenter = arenaCenter;
            Agent = agent;
            Baseline = baseline;
        }

        /// <summary>Atomic pair-reset to the (runSeed, episodeIndex) poses, flushing in-flight projectiles.</summary>
        public void Reset(in RewardSpec spec, int episodeIndex)
        {
            var poses = EpisodePoses.Derive(in spec, episodeIndex, arenaCenter);
            units.RespawnShip(Agent.Id, poses.agentPos, poses.agentRotDeg);
            units.RespawnShip(Baseline.Id, poses.baselinePos, poses.baselineRotDeg);
            projectiles.ReturnAllToPool();
        }

        public void Dispose()
        {
            Remove(Agent);
            Remove(Baseline);
        }

        private void Remove(Ship ship)
        {
            if (!ship) return;
            units.ActiveRegistry.ActiveShips.Remove(ship);
            UnityEngine.Object.DestroyImmediate(ship.gameObject);
        }
    }
}

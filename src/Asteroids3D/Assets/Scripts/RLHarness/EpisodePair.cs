using System;
using AI;
using Game.Services;
using Ships;
using Ships.Command;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>The canonical 1v1 episode composition: agent ship on the inert TestPilotMPC host (its Navigator authors MpcSettings_AgentPilot — the policy-matched tracker config) with an injected chooser, versus a fixed mid-band brawler baseline (<see cref="HoldRangeFireChooser"/>); both lasers-only. Hosts (tests, training scene) share this so the scenario cannot drift between them.</summary>
    public sealed class EpisodePair : IDisposable
    {
        private const uint AgentSeedStream = 101;
        private const uint BaselineSeedStream = 202;

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

        /// <summary>Spawns the pair at the (runSeed, episode 0) poses; the chooser factory sees both live ships so it can configure itself (injected opponent, projectile speed) before the commanders initialize.</summary>
        public static EpisodePair Spawn(UnitService units, ArenaContext arena, IProjectileService projectiles,
            in RewardSpec spec, Func<Ship, Ship, IIntentChooser> chooserFactory, HarnessAssets assets)
        {
            var poses = EpisodePoses.Derive(in spec, 0, arena.Offset);
            var rootScope = new SeedScope(spec.runSeed);

            var agent = SpawnLasersOnlyShip(units, projectiles, assets.ShipPrefab, assets.AgentPilot,
                poses.agentPos, poses.agentRotDeg, team: 0, rootScope.Derive(AgentSeedStream).ToSeed());
            var baseline = SpawnLasersOnlyShip(units, projectiles, assets.ShipPrefab, assets.BaselinePilot,
                poses.baselinePos, poses.baselineRotDeg, team: 1, rootScope.Derive(BaselineSeedStream).ToSeed());

            var commander = agent.GetComponentInChildren<AICommander>();
            var chooser = chooserFactory(agent, baseline);
            commander.GetComponentInChildren<Brain>().InstallChooser(chooser);

            // Fixed mid-band Aggressor draw: the deterministic default opponent; roster episodes re-install per draw.
            var baselineChooser = new HoldRangeFireChooser();
            baselineChooser.Configure(agent, desiredRange: 10f, speedFraction: 0.85f,
                baseline.Weapons.Context.ProjectileSpeed(WeaponSlot.Primary), arena.Offset, spec.arenaRadius);
            baseline.GetComponentInChildren<Brain>().InstallChooser(baselineChooser);

            units.WireShipDependencies(agent);
            units.WireShipDependencies(baseline);

            return new EpisodePair(units, projectiles, arena.Offset, agent, baseline);
        }

        /// <summary>The canonical ShipAgent composition: pair plus a configured <see cref="AgentChooser"/> (injected opponent) — the single recipe every agent host (training, eval, tests) shares.</summary>
        public static EpisodePair SpawnWithAgentChooser(UnitService units, ArenaContext arena,
            IProjectileService projectiles, in RewardSpec spec, HarnessAssets assets, out AgentChooser chooser)
        {
            AgentChooser created = null;
            var pair = Spawn(units, arena, projectiles, in spec, (agentShip, baselineShip) =>
            {
                created = new AgentChooser();
                created.Configure(baselineShip);
                return created;
            }, assets);
            chooser = created;
            return pair;
        }

        /// <summary>The self-play composition: BOTH ships on the agent pilot (TestPilotMPC), each driven by its own <see cref="AgentChooser"/> injected with the OTHER ship as opponent. Poses/seeds derive exactly as <see cref="Spawn"/> (agent = team 0 / stream 101, baseline slot = team 1 / stream 202), so the mirror ship starts from the canonical baseline pose. No scripted baseline — both ships are agent-driven.</summary>
        public static EpisodePair SpawnSelfPlayPair(UnitService units, ArenaContext arena,
            IProjectileService projectiles, in RewardSpec spec, HarnessAssets assets,
            out AgentChooser chooserA, out AgentChooser chooserB)
        {
            var poses = EpisodePoses.Derive(in spec, 0, arena.Offset);
            var rootScope = new SeedScope(spec.runSeed);

            var shipA = SpawnLasersOnlyShip(units, projectiles, assets.ShipPrefab, assets.AgentPilot,
                poses.agentPos, poses.agentRotDeg, team: 0, rootScope.Derive(AgentSeedStream).ToSeed());
            var shipB = SpawnLasersOnlyShip(units, projectiles, assets.ShipPrefab, assets.AgentPilot,
                poses.baselinePos, poses.baselineRotDeg, team: 1, rootScope.Derive(BaselineSeedStream).ToSeed());

            chooserA = InstallAgentChooser(shipA, opponent: shipB);
            chooserB = InstallAgentChooser(shipB, opponent: shipA);

            units.WireShipDependencies(shipA);
            units.WireShipDependencies(shipB);
            return new EpisodePair(units, projectiles, arena.Offset, shipA, shipB);
        }

        private static AgentChooser InstallAgentChooser(Ship ship, Ship opponent)
        {
            var chooser = new AgentChooser();
            chooser.Configure(opponent);
            var commander = ship.GetComponentInChildren<AICommander>();
            commander.GetComponentInChildren<Brain>().InstallChooser(chooser);
            return chooser;
        }

        /// <summary>Atomic pair-reset to the (runSeed, episodeIndex) poses, flushing in-flight projectiles.</summary>
        public SpawnPoses Reset(in RewardSpec spec, int episodeIndex)
        {
            var poses = EpisodePoses.Derive(in spec, episodeIndex, arenaCenter);
            units.RespawnShip(Agent.Id, poses.agentPos, poses.agentRotDeg);
            units.RespawnShip(Baseline.Id, poses.baselinePos, poses.baselineRotDeg);
            projectiles.ReturnAllToPool();
            return poses;
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

        /// <summary>Also the traversal probe's single-ship recipe — probe crossings fly the exact combat-episode airframe/loadout.</summary>
        internal static Ship SpawnLasersOnlyShip(UnitService units, IProjectileService projectiles,
            Ship shipPrefab, AICommander pilot, Vector2 planePos, float rotDeg, int team, int decisionSeed)
        {
            var ship = Factory.CreateShip(shipPrefab, pilot, team, decisionSeed, projectiles,
                GamePlane.PlanePointToWorld(planePos),
                GamePlane.Rotation * Quaternion.AngleAxis(rotDeg, Vector3.forward));
            // Home the pair under the service like SpawnShip does, so a crash-path host teardown can't strand it.
            ship.transform.SetParent(units.transform, true);
            units.ActiveRegistry.ActiveShips.Add(ship);

            ship.Reequip(ship.Engine, ship.Shield, ship.Weapons.PrimaryMountPrefab, null);
            if (ship.Weapons.Context.Slots.Count != 1)
                throw new InvalidOperationException("Episode loadout must be lasers-only.");
            return ship;
        }
    }
}

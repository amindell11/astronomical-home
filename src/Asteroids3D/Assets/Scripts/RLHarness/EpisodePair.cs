using System;
using AI;
using Game.Services;
using Ships;
using Ships.Command;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.RLHarness
{
    /// <summary>The canonical 1v1 episode composition: agent ship on the inert TestPilotMPC host (its Navigator authors MpcSettings_AgentPilot — the policy-matched tracker config) with an injected chooser, versus the full production UtilityPilot baseline; both lasers-only. Hosts (tests, training scene) share this so the scenario cannot drift between them.</summary>
    public sealed class EpisodePair : IDisposable
    {
        private const string ShipPrefabPath = "Assets/Prefabs/Ships/Ship_2.prefab";
        internal const string AgentPilotPath = "Assets/Prefabs/Pilots/TestPilotMPC.prefab";
        private const string BaselinePilotPath = "Assets/Prefabs/Pilots/UtilityPilot.prefab";
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
            in RewardSpec spec, Func<Ship, Ship, IIntentChooser> chooserFactory)
        {
            var poses = EpisodePoses.Derive(in spec, 0, arena.Offset);
            var rootScope = new SeedScope(spec.runSeed);

            var agent = SpawnLasersOnlyShip(units, projectiles, AgentPilotPath,
                poses.agentPos, poses.agentRotDeg, team: 0, rootScope.Derive(AgentSeedStream).ToSeed());
            var baseline = SpawnLasersOnlyShip(units, projectiles, BaselinePilotPath,
                poses.baselinePos, poses.baselineRotDeg, team: 1, rootScope.Derive(BaselineSeedStream).ToSeed());

            var commander = agent.GetComponentInChildren<AICommander>();
            var chooser = chooserFactory(agent, baseline);
            commander.GetComponentInChildren<Brain>().InstallChooser(chooser);

            units.WireShipDependencies(agent);
            units.WireShipDependencies(baseline);
            if (baseline.GetComponentInChildren<AICommander>().CurrentStateName == "None")
                throw new InvalidOperationException("Baseline brain must run a real state policy — check the UtilityPilot prefab's state profiles.");

            return new EpisodePair(units, projectiles, arena.Offset, agent, baseline);
        }

        /// <summary>The canonical ShipAgent composition: pair plus a configured <see cref="AgentChooser"/> (injected opponent, primary projectile speed) — the single recipe every agent host (training, eval, tests) shares.</summary>
        public static EpisodePair SpawnWithAgentChooser(UnitService units, ArenaContext arena,
            IProjectileService projectiles, in RewardSpec spec, out AgentChooser chooser)
        {
            AgentChooser created = null;
            var pair = Spawn(units, arena, projectiles, in spec, (agentShip, baselineShip) =>
            {
                created = new AgentChooser();
                created.Configure(baselineShip,
                    agentShip.Weapons.Context.ProjectileSpeed(WeaponSlot.Primary));
                return created;
            });
            chooser = created;
            return pair;
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
            string pilotPath, Vector2 planePos, float rotDeg, int team, int decisionSeed)
        {
            var shipPrefab = Load<Ship>(ShipPrefabPath);
            var pilotPrefab = Load<AICommander>(pilotPath);
            var ship = Factory.CreateShip(shipPrefab, pilotPrefab, team, decisionSeed, projectiles,
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

        private static T Load<T>(string assetPath) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (!asset)
                throw new InvalidOperationException($"Failed to load {assetPath} — check episode asset paths.");
            return asset;
#else
            throw new NotSupportedException("EpisodePair composition loads prefabs via AssetDatabase (editor only).");
#endif
        }
    }
}

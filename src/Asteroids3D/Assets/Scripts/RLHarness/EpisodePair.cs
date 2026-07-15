using System;
using AI;
using Game.Services;
using Movement.MPC;
using Ships;
using Ships.Command;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.RLHarness
{
    /// <summary>The canonical 1v1 episode composition (PR-2b scenario): agent ship on the inert TestPilotMPC host with an injected chooser and a private MpcSettings clone (wVelTrack raised, boost sampling zeroed — boost is policy-owned), versus the full production UtilityPilot baseline; both lasers-only. Hosts (tests, training scene) share this so the scenario cannot drift between them.</summary>
    public sealed class EpisodePair : IDisposable
    {
        public const float AgentWVelTrack = 50f;
        private const string ShipPrefabPath = "Assets/Prefabs/Ships/Ship_2.prefab";
        private const string AgentPilotPath = "Assets/Prefabs/Pilots/TestPilotMPC.prefab";
        private const string BaselinePilotPath = "Assets/Prefabs/Pilots/UtilityPilot.prefab";
        private const uint AgentSeedStream = 101;
        private const uint BaselineSeedStream = 202;

        public Ship Agent { get; }
        public Ship Baseline { get; }

        private readonly UnitService units;
        private readonly Vector2 arenaCenter;
        private readonly MpcSettings agentSettings;

        private EpisodePair(UnitService units, Vector2 arenaCenter, Ship agent, Ship baseline,
            MpcSettings agentSettings)
        {
            this.units = units;
            this.arenaCenter = arenaCenter;
            Agent = agent;
            Baseline = baseline;
            this.agentSettings = agentSettings;
        }

        /// <summary>Spawns the pair at the (runSeed, episode 0) poses; the chooser factory sees both live ships so it can configure itself (injected opponent, projectile speed) before the commanders initialize.</summary>
        public static EpisodePair Spawn(UnitService units, ArenaContext arena, in RewardSpec spec,
            Func<Ship, Ship, IIntentChooser> chooserFactory)
        {
            var poses = EpisodePoses.Derive(in spec, 0, arena.Offset);
            var rootScope = new SeedScope(spec.runSeed);

            var agent = SpawnLasersOnlyShip(units, AgentPilotPath,
                poses.agentPos, poses.agentRotDeg, team: 0, rootScope.Derive(AgentSeedStream).ToSeed());
            var baseline = SpawnLasersOnlyShip(units, BaselinePilotPath,
                poses.baselinePos, poses.baselineRotDeg, team: 1, rootScope.Derive(BaselineSeedStream).ToSeed());

            var commander = agent.GetComponentInChildren<AICommander>();
            var navigator = commander.Navigator;
            var settings = UnityEngine.Object.Instantiate(
                navigator.mpcSettings ? navigator.mpcSettings : ScriptableObject.CreateInstance<MpcSettings>());
            settings.wVelTrack = AgentWVelTrack;
            settings.boostSampleProbability = 0f;
            navigator.mpcSettings = settings;

            var chooser = chooserFactory(agent, baseline);
            commander.GetComponentInChildren<Brain>().InstallChooser(chooser);

            units.WireShipDependencies(agent);
            units.WireShipDependencies(baseline);
            if (baseline.GetComponentInChildren<AICommander>().CurrentStateName == "None")
                throw new InvalidOperationException("Baseline brain must run a real state policy — check the UtilityPilot prefab's state profiles.");

            return new EpisodePair(units, arena.Offset, agent, baseline, settings);
        }

        /// <summary>The canonical ShipAgent composition: pair plus a configured <see cref="AgentChooser"/> (injected opponent, primary projectile speed) — the single recipe every agent host (training, eval, tests) shares.</summary>
        public static EpisodePair SpawnWithAgentChooser(UnitService units, ArenaContext arena,
            in RewardSpec spec, out AgentChooser chooser)
        {
            AgentChooser created = null;
            var pair = Spawn(units, arena, in spec, (agentShip, baselineShip) =>
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
            ProjectileFlush.ReturnAllToPool();
            return poses;
        }

        public void Dispose()
        {
            Remove(Agent);
            Remove(Baseline);
            if (agentSettings) UnityEngine.Object.DestroyImmediate(agentSettings);
        }

        private void Remove(Ship ship)
        {
            if (!ship) return;
            units.ActiveRegistry.ActiveShips.Remove(ship);
            UnityEngine.Object.DestroyImmediate(ship.gameObject);
        }

        private static Ship SpawnLasersOnlyShip(UnitService units, string pilotPath,
            Vector2 planePos, float rotDeg, int team, int decisionSeed)
        {
            var shipPrefab = Load<Ship>(ShipPrefabPath);
            var pilotPrefab = Load<AICommander>(pilotPath);
            var ship = Factory.CreateShip(shipPrefab, pilotPrefab, team, decisionSeed,
                GamePlane.PlanePointToWorld(planePos),
                GamePlane.Rotation * Quaternion.AngleAxis(rotDeg, Vector3.forward));
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

using System;
using AI;
using AI.Tactics;
using Ships;
using Ships.Loadout;
using UnityEngine;
using Substrate.Services.Units;
using Substrate.Services.Projectiles;
using RL.Episodes;
using RL.Hosts;
using RL.Hosts.Lanes;
using RL.Opponents;
using RL.Reward;

namespace RL.Episodes.Compositions
{
    /// <summary>The matrix lane's harness composition. The weapon-pair path installs a fixed Aggressor on both ships; a roster side flies its pilot prefab's authored brain, and a break-off override hands the reference a runtime copy of its tactics. The loadout binds at spawn, so one seed's episodes share the ships and an odd episode only swaps poses.</summary>
    internal sealed class MatrixComposition : IHarnessComposition
    {
        private static readonly OpponentDraw ShooterShape = new() { desiredRange = 10f, speedFraction = 0.85f };

        public EpisodeLoopDriver Driver { get; }
        public EpisodePair Pair { get; }

        private readonly UnitService units;
        private readonly AICommander agentCommander;
        private readonly AICommander baselineCommander;
        private readonly bool installAggressor;
        private readonly string rosterPilotName;
        private readonly Vector2 arenaCenter;
        private readonly ShieldModule runtimeShieldA;
        private readonly ShieldModule runtimeShieldB;
        private readonly Ship[] extras;
        private readonly TacticsSettings breakOffClone;
        private SpawnPoses poses;
        private bool swapPending;
        private bool applyingSwap;
        private int respawnCount;

        public MatrixComposition(UnitService units, Vector2 offset, IProjectileService projectiles,
            in RewardSpec spec, MatrixSide sideA, MatrixSide sideB, bool installAggressor, bool kitsOff = false,
            int enemies = 1, bool breakOffSet = false, float breakOff = 0f)
        {
            this.units = units;
            this.installAggressor = installAggressor;
            rosterPilotName = sideB.pilot.name;
            arenaCenter = offset;
            Pair = MatrixSpawner.Spawn(units, offset, field: null, projectiles, in spec, sideA, sideB,
                enemies, out extras, out runtimeShieldA, out runtimeShieldB);
            agentCommander = Pair.Agent.GetComponentInChildren<AICommander>();
            baselineCommander = Pair.Baseline.GetComponentInChildren<AICommander>();
            if (kitsOff)
            {
                agentCommander.suppressKeystone = true;
                baselineCommander.suppressKeystone = true;
                for (var i = 0; i < extras.Length; i++)
                    extras[i].GetComponentInChildren<AICommander>().suppressKeystone = true;
            }
            if (breakOffSet) breakOffClone = ApplyBreakOff(agentCommander, breakOff);
            Driver = new EpisodeLoopDriver(Pair, offset);
            Driver.TrackExtraOpponents(extras);
            units.OnShipRespawned += OnRespawned;
        }

        public OpponentDraw InstallOpponent(in RewardSpec spec, int episodeIndex)
        {
            OpponentDraw draw;
            if (installAggressor)
            {
                var arenaRadius = spec.arenaRadius;
                agentCommander.InstallBrain<ArchetypeBrain>()
                    .Configure(Pair.Baseline, OpponentArchetype.Aggressor, in ShooterShape, jukeSeed: 0,
                        arenaCenter, arenaRadius);
                baselineCommander.InstallBrain<ArchetypeBrain>()
                    .Configure(Pair.Agent, OpponentArchetype.Aggressor, in ShooterShape, jukeSeed: 0,
                        arenaCenter, arenaRadius);
                draw = new OpponentDraw
                {
                    archetype = OpponentArchetype.Aggressor.ToString(),
                    desiredRange = ShooterShape.desiredRange,
                    speedFraction = ShooterShape.speedFraction,
                };
            }
            else
            {
                draw = new OpponentDraw { archetype = rosterPilotName };
            }

            poses = EpisodePoses.Derive(in spec, MatrixLane.PoseIndex(episodeIndex), arenaCenter);
            swapPending = MatrixLane.SidesSwapped(episodeIndex);
            respawnCount = 0;
            return draw;
        }

        public void Dispose()
        {
            if (units) units.OnShipRespawned -= OnRespawned;
            for (var i = 0; i < extras.Length; i++) Remove(extras[i]);
            Pair.Dispose();
            if (runtimeShieldA) UnityEngine.Object.DestroyImmediate(runtimeShieldA);
            if (runtimeShieldB) UnityEngine.Object.DestroyImmediate(runtimeShieldB);
            if (breakOffClone) UnityEngine.Object.DestroyImmediate(breakOffClone);
        }

        private static TacticsSettings ApplyBreakOff(AICommander commander, float breakOff)
        {
            var brain = commander.Brain as ArchetypeBrain;
            if (!brain)
                throw new InvalidOperationException(
                    $"{commander.name}: a break-off override needs an archetype brain");
            if (!brain.AuthoredTactics)
                throw new InvalidOperationException(
                    $"{commander.name}: a break-off override needs authored tactics settings");
            var clone = UnityEngine.Object.Instantiate(brain.AuthoredTactics);
            clone.hideFlags = HideFlags.HideAndDontSave;
            clone.breakOffShield = breakOff;
            brain.ReplaceTactics(clone);
            return clone;
        }

        private void Remove(Ship ship)
        {
            if (!ship) return;
            units.ActiveRegistry.ActiveShips.Remove(ship);
            UnityEngine.Object.DestroyImmediate(ship.gameObject);
        }

        // Odd episodes swap sides; extras then follow the baseline around the reference.
        private void OnRespawned(Ship ship)
        {
            if (applyingSwap || IsExtra(ship)) return;
            if (++respawnCount < 2) return;
            respawnCount = 0;
            if (!swapPending && extras.Length == 0) return;
            applyingSwap = true;
            var referencePos = poses.agentPos;
            var baselinePos = poses.baselinePos;
            if (swapPending)
            {
                swapPending = false;
                units.RespawnShip(Pair.Agent.Id, poses.baselinePos, poses.baselineRotDeg);
                units.RespawnShip(Pair.Baseline.Id, poses.agentPos, poses.agentRotDeg);
                referencePos = poses.baselinePos;
                baselinePos = poses.agentPos;
            }
            for (var i = 0; i < extras.Length; i++)
            {
                var pose = EpisodePoses.ExtraOnBaselineCircle(referencePos, baselinePos, i);
                units.RespawnShip(extras[i].Id, pose.pos, pose.rotDeg);
            }
            applyingSwap = false;
        }

        private bool IsExtra(Ship ship)
        {
            for (var i = 0; i < extras.Length; i++)
                if (extras[i] == ship) return true;
            return false;
        }
    }
}

using System;
using System.Reflection;
using AI;
using AI.Scanning;
using Combat.Weapons;
using Ships;
using UnityEngine;
using Utils;
using Substrate;
using Substrate.Services.Units;
using Substrate.Services.Projectiles;
using RL.Hosts;
using RL.Reward;

namespace RL.Episodes
{
    /// <summary>Spawns the matrix lane's pair. Each side is a chassis prefab, a pilot prefab, and one primary weapon. The weapon is equipped before wiring, because the commander captures muzzle speed once at init.</summary>
    internal static class MatrixSpawner
    {
        // EpisodePair's armed-pair streams. A seed's decision noise matches that spawn.
        private const uint AgentSeedStream = 101;
        private const uint BaselineSeedStream = 202;

        public static EpisodePair Spawn(UnitService units, Vector2 offset, IObstacleField field,
            IProjectileService projectiles, in RewardSpec spec, MatrixSide sideA, MatrixSide sideB)
        {
            var poses = EpisodePoses.Derive(in spec, 0, offset);
            var root = new SeedScope(spec.runSeed);
            var shipA = SpawnShip(units, projectiles, sideA.chassis, sideA.pilot,
                poses.agentPos, poses.agentRotDeg, team: 0, root.Derive(AgentSeedStream).ToSeed(), sideA.weapon);
            var shipB = SpawnShip(units, projectiles, sideB.chassis, sideB.pilot,
                poses.baselinePos, poses.baselineRotDeg, team: 1, root.Derive(BaselineSeedStream).ToSeed(), sideB.weapon);
            units.WireShipDependencies(shipA, field);
            units.WireShipDependencies(shipB, field);
            return Seal(units, projectiles, offset, shipA, shipB);
        }

        private static Ship SpawnShip(UnitService units, IProjectileService projectiles, Ship shipPrefab,
            AICommander pilot, Vector2 planePos, float rotDeg, int team, int decisionSeed, WeaponComponent primary)
        {
            var ship = Factory.CreateShip(shipPrefab, pilot, team, decisionSeed, projectiles,
                GamePlane.PlanePointToWorld(planePos),
                GamePlane.Rotation * Quaternion.AngleAxis(rotDeg, Vector3.forward));
            ship.transform.SetParent(units.UnitsRoot, true);
            units.ActiveRegistry.ActiveShips.Add(ship);
            ship.Reequip(primary, null);
            return ship;
        }

        private static EpisodePair Seal(UnitService units, IProjectileService projectiles, Vector2 arenaCenter,
            Ship agent, Ship baseline)
        {
            var ctor = PairConstructor();
            return (EpisodePair)ctor.Invoke(new object[] { units, projectiles, arenaCenter, agent, baseline });
        }

        private static ConstructorInfo PairConstructor()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            foreach (var ctor in typeof(EpisodePair).GetConstructors(flags))
            {
                var parameters = ctor.GetParameters();
                if (parameters.Length != 5) continue;
                if (parameters[0].ParameterType != typeof(UnitService)) continue;
                if (parameters[1].ParameterType != typeof(IProjectileService)) continue;
                if (parameters[2].ParameterType != typeof(Vector2)) continue;
                if (parameters[3].ParameterType != typeof(Ship)) continue;
                if (parameters[4].ParameterType != typeof(Ship)) continue;
                return ctor;
            }
            throw new InvalidOperationException(
                "MatrixSpawner could not find EpisodePair's constructor (UnitService, IProjectileService, Vector2, Ship, Ship).");
        }
    }
}

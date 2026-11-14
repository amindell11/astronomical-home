using System;
using System.Reflection;
using AI;
using AI.Scanning;
using Combat.Weapons;
using Ships;
using Ships.Loadout;
using UnityEngine;
using Utils;
using Substrate;
using Substrate.Services.Units;
using Substrate.Services.Projectiles;
using RL.Hosts;
using RL.Reward;

namespace RL.Episodes
{
    /// <summary>Spawns the matrix lane's pair. Each side is a chassis prefab, a pilot prefab, and one primary weapon. A named shield or numeric capacity/delay/rate replaces the chassis module on an inactive copy used as the spawn template. The weapon is equipped before wiring, because the commander captures muzzle speed once at init.</summary>
    internal static class MatrixSpawner
    {
        // EpisodePair's armed-pair streams. A seed's decision noise matches that spawn.
        private const uint AgentSeedStream = 101;
        private const uint BaselineSeedStream = 202;

        public static EpisodePair Spawn(UnitService units, Vector2 offset, IObstacleField field,
            IProjectileService projectiles, in RewardSpec spec, MatrixSide sideA, MatrixSide sideB,
            out ShieldModule runtimeShieldA, out ShieldModule runtimeShieldB)
        {
            var poses = EpisodePoses.Derive(in spec, 0, offset);
            var root = new SeedScope(spec.runSeed);
            var shipA = SpawnShip(units, projectiles, sideA,
                poses.agentPos, poses.agentRotDeg, team: 0, root.Derive(AgentSeedStream).ToSeed(), out runtimeShieldA);
            var shipB = SpawnShip(units, projectiles, sideB,
                poses.baselinePos, poses.baselineRotDeg, team: 1, root.Derive(BaselineSeedStream).ToSeed(),
                out runtimeShieldB);
            units.WireShipDependencies(shipA, field);
            units.WireShipDependencies(shipB, field);
            return Seal(units, projectiles, offset, shipA, shipB);
        }

        private static Ship SpawnShip(UnitService units, IProjectileService projectiles, in MatrixSide side,
            Vector2 planePos, float rotDeg, int team, int decisionSeed, out ShieldModule runtimeShield)
        {
            var shield = side.shield;
            runtimeShield = null;
            if (side.shieldNumbers)
            {
                runtimeShield = ScriptableObject.CreateInstance<ShieldModule>();
                runtimeShield.hideFlags = HideFlags.HideAndDontSave;
                runtimeShield.maxShield = side.shieldCapacity;
                runtimeShield.shieldRegenDelay = side.shieldDelay;
                runtimeShield.shieldRegenRate = side.shieldRate;
                shield = runtimeShield;
            }
            var template = ChassisTemplate(side.chassis, shield);
            try
            {
                var ship = Factory.CreateShip(template, side.pilot, team, decisionSeed, projectiles,
                    GamePlane.PlanePointToWorld(planePos),
                    GamePlane.Rotation * Quaternion.AngleAxis(rotDeg, Vector3.forward));
                ship.transform.SetParent(units.UnitsRoot, true);
                units.ActiveRegistry.ActiveShips.Add(ship);
                ship.Reequip(side.weapon, null);
                return ship;
            }
            finally
            {
                if (template != side.chassis)
                    UnityEngine.Object.DestroyImmediate(template.transform.parent.gameObject);
            }
        }

        private static Ship ChassisTemplate(Ship prefab, ShieldModule shield)
        {
            if (!shield) return prefab;
            var holder = new GameObject("matrix-chassis-template");
            holder.hideFlags = HideFlags.HideAndDontSave;
            holder.SetActive(false);
            // Under an inactive holder the copy never wakes, so it stays a plain template.
            var copy = UnityEngine.Object.Instantiate(prefab, holder.transform);
            copy.shield = shield;
            return copy;
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

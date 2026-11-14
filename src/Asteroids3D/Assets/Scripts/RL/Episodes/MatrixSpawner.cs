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
    /// <summary>Spawns the matrix lane's ships. A named shield or numeric capacity/delay/rate replaces the chassis module on an inactive copy used as the spawn template. The weapon is equipped before wiring, because the commander captures muzzle speed once at init.</summary>
    internal static class MatrixSpawner
    {
        // EpisodePair's armed-pair streams. A seed's decision noise matches that spawn.
        private const uint AgentSeedStream = 101;
        private const uint BaselineSeedStream = 202;
        private const uint FirstExtraSeedStream = 303;
        private const uint SecondExtraSeedStream = 404;

        public static EpisodePair Spawn(UnitService units, Vector2 offset, IObstacleField field,
            IProjectileService projectiles, in RewardSpec spec, MatrixSide sideA, MatrixSide sideB,
            int enemies, out Ship[] extras, out ShieldModule runtimeShieldA, out ShieldModule runtimeShieldB)
        {
            if (enemies < 1 || enemies > 3)
                throw new ArgumentOutOfRangeException(nameof(enemies), enemies,
                    "a roster matrix fights 1, 2, or 3 copies");
            var poses = EpisodePoses.Derive(in spec, 0, offset);
            var root = new SeedScope(spec.runSeed);
            var shieldA = ShieldFor(sideA, out runtimeShieldA);
            var shieldB = ShieldFor(sideB, out runtimeShieldB);
            var shipA = SpawnShip(units, projectiles, sideA,
                poses.agentPos, poses.agentRotDeg, team: 0, root.Derive(AgentSeedStream).ToSeed(), shieldA);
            var shipB = SpawnShip(units, projectiles, sideB,
                poses.baselinePos, poses.baselineRotDeg, team: 1, root.Derive(BaselineSeedStream).ToSeed(), shieldB);
            units.WireShipDependencies(shipA, field);
            units.WireShipDependencies(shipB, field);
            extras = new Ship[enemies - 1];
            for (var i = 0; i < extras.Length; i++)
            {
                var pose = EpisodePoses.ExtraOnBaselineCircle(poses.agentPos, poses.baselinePos, i);
                extras[i] = SpawnShip(units, projectiles, sideB, pose.pos, pose.rotDeg, team: 1,
                    root.Derive(ExtraSeedStream(i)).ToSeed(), shieldB);
                units.WireShipDependencies(extras[i], field);
            }
            return Seal(units, projectiles, offset, shipA, shipB);
        }

        private static uint ExtraSeedStream(int index) => index switch
        {
            0 => FirstExtraSeedStream,
            1 => SecondExtraSeedStream,
            _ => throw new ArgumentOutOfRangeException(nameof(index), index, null),
        };

        private static ShieldModule ShieldFor(in MatrixSide side, out ShieldModule runtime)
        {
            runtime = null;
            if (!side.shieldNumbers) return side.shield;
            runtime = ScriptableObject.CreateInstance<ShieldModule>();
            runtime.hideFlags = HideFlags.HideAndDontSave;
            runtime.maxShield = side.shieldCapacity;
            runtime.shieldRegenDelay = side.shieldDelay;
            runtime.shieldRegenRate = side.shieldRate;
            return runtime;
        }

        private static Ship SpawnShip(UnitService units, IProjectileService projectiles, in MatrixSide side,
            Vector2 planePos, float rotDeg, int team, int decisionSeed, ShieldModule shield)
        {
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

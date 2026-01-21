using System;
using AI.Computers;
using Editor;
using Unity.Properties;
using UnityEngine;
using Ships.Control;
namespace AI.Context
{
    /// <summary>
    /// Provides on-demand AI context data for AI state machine consumption.
    /// Composes ShipInfo, CombatInfo, and Navigation providers for focused responsibilities.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    public partial class Info
    {
        private Ships.Ship ship;

        // Data providers
        public ShipInfo Ship { get; private set; }
        public Combat Combat { get; private set; }
        public Navigation Nav { get; private set; }

        // Computers (heavy calculations)
        public Targeting Targeting { get; private set; }
        public Scout Scout { get; private set; }
        public Maneuvers Maneuvers { get; private set; }

        public Info(Ships.Ship ship, Navigator navigator, Gunner gunner, Scout scout, Targeting targeting, Maneuvers maneuvers)
        {
            this.ship = ship;
            if (!ship)
            {
                return;
            }

            Ship = new ShipInfo(ship);
            Targeting = targeting;
            Scout = scout;
            Maneuvers = maneuvers;
            Combat = new Combat(Scout, gunner, Targeting);
            Nav = new Navigation(Ship, Scout, navigator);
        }

        // Ship state
        public Vector2 SelfPosition => Ship?.Pos ?? Vector2.zero;
        public Vector3 SelfPosition3D => Ship?.Pos3D ?? (ship ? ship.transform.position : Vector3.zero);
        public Transform SelfTransform => ship ? ship.transform : null;
        public Vector2 SelfVelocity => Ship?.Vel ?? Vector2.zero;
        public float SpeedPct => Ship?.SpeedPct ?? 0f;
        public Vector2 SelfForward => Ship?.Forward ?? Vector2.up;
        public float ShieldPct => Ship?.ShieldPct ?? 0f;
        public float HealthPct => Ship?.HealthPct ?? 0f;

        // Enemy state (from Combat)
        public bool InCombat => Combat?.InCombat ?? false;
        public Ships.Ship Enemy => Combat?.Enemy;
        public Vector2 EnemyPos => Combat?.EnemyPos ?? Vector2.zero;
        public Vector2 EnemyVel => Combat?.EnemyVel ?? Vector2.zero;
        public float EnemyHealthPct => Combat?.EnemyHealthPct ?? 0f;
        public float EnemyShieldPct => Combat?.EnemyShieldPct ?? 0f;

        // Targeting calculations (via Targeting computer)
        public Vector2 VectorToEnemy => Targeting?.VectorTo(EnemyPos) ?? Vector2.zero;
        public Vector2 EnemyRelVelocity => Targeting?.RelativeVelocity(EnemyVel) ?? Vector2.zero;
        public float ClosingSpeed => Targeting?.ClosingSpeed(EnemyPos, EnemyVel) ?? 0f;
        public bool LineOfSightToEnemy => Enemy && (Targeting?.HasLineOfSight(Enemy.transform.position) ?? false);
        public float AngleToEnemy => Targeting?.AngleTo(EnemyPos) ?? 0f;
        public float EnemyAngleToSelf => Enemy ? (Targeting?.AngleFromTarget(EnemyPos, Combat.EnemyForward) ?? 180f) : 180f;
        public float SelfAngleToEnemy => AngleToEnemy;

        // Gunner target accessors
        public Vector2 VectorToTarget => Combat?.VectorToTarget ?? Vector2.zero;
        public bool HasTargetLos => Combat?.HasTargetLos ?? false;
        public float AngleToTarget => Combat?.AngleToTarget ?? 0f;
        public bool IncomingMissile => Combat?.IncomingMissile ?? false;
        public float LaserSpeed => Combat?.LaserSpeed ?? 0f;

        // Scout analysis results
        public int NearbyEnemyCount => Scout?.Ships?.EnemyCount ?? 0;
        public float NearestThreatDistance => Scout?.Ships?.NearestThreatDistance() ?? float.MaxValue;
        public int NearbyFriendCount => Scout?.Ships?.FriendCount ?? 0;

        // Backward-compatible accessors delegating to Navigation
        public Vector2 VectorToWaypoint => Nav?.VectorToWaypoint ?? Vector2.zero;
        public bool NearAsteroidCover => Nav?.NearAsteroidCover ?? false;

        public override string ToString()
        {
            return $"AIContext[Shield:{ShieldPct:F2} Health:{HealthPct:F2} " +
                   $"EnemyDist:{VectorToEnemy.magnitude:F1} LOS:{LineOfSightToEnemy} " +
                   $"Enemies:{NearbyEnemyCount} Friends:{NearbyFriendCount}]";
        }
    }
}

using Ships;
using UnityEngine;
using TargetingUtils = Combat.TargetingUtils;

namespace AI.Context
{
    public class CombatTracker
    {
        private readonly Scanning.Scout scout;
        private readonly Gunner gunner;
        private readonly TargetingUtils targeting;
        private readonly ShipId selfId;
        private readonly IShipRegistry registry;
        private readonly float combatExitDelay;

        private Ship cachedEnemy;
        private float lastContactTime;

        public CombatTracker(
            Scanning.Scout scout,
            Gunner gunner,
            TargetingUtils targeting,
            ShipId selfId,
            IShipRegistry registry,
            float combatExitDelay)
        {
            this.scout = scout;
            this.gunner = gunner;
            this.targeting = targeting;
            this.selfId = selfId;
            this.registry = registry;
            this.combatExitDelay = combatExitDelay;
        }

        public Ship Enemy => cachedEnemy;
        public bool HasEnemy => cachedEnemy && cachedEnemy.gameObject.activeInHierarchy;
        public bool InCombat => HasEnemy || TimeSinceCombat < combatExitDelay;
        public float TimeSinceCombat { get; private set; }

        public Vector2 EnemyPos => HasEnemy ? cachedEnemy.CurrentState.kinematics.pos : Vector2.zero;
        public Vector2 EnemyVel => HasEnemy ? cachedEnemy.CurrentState.kinematics.vel : Vector2.zero;
        public Vector2 EnemyForward => HasEnemy ? cachedEnemy.CurrentState.kinematics.Forward : Vector2.up;
        public float EnemyHealthPct => HasEnemy ? cachedEnemy.CurrentState.healthPct : 0f;
        public float EnemyShieldPct => HasEnemy ? cachedEnemy.CurrentState.shieldPct : 0f;

        public Vector2 VectorToTarget => gunner?.VectorToTarget ?? Vector2.zero;
        public bool HasTargetLos => gunner?.HasTargetLos ?? false;
        public float AngleToTarget => gunner?.AngleToTarget ?? 0f;
        public float LaserSpeed => gunner?.PrimaryProjectileSpeed ?? 0f;
        public bool IncomingMissile => false; // TODO

        public void Update()
        {
            if (cachedEnemy == null || !cachedEnemy.gameObject.activeInHierarchy)
            {
                cachedEnemy = null;
                AcquireEnemy();
            }

            if (HasEnemy)
            {
                lastContactTime = Time.time;
                TimeSinceCombat = 0f;
            }
            else
            {
                TimeSinceCombat = lastContactTime > 0f ? Time.time - lastContactTime : float.MaxValue;
            }
        }

        private void AcquireEnemy()
        {
            if (registry == null || !registry.TryGetShip(selfId, out var selfShip))
                return;

            var scan = scout.ShipScan;
            if (scan == null) return;

            var enemyId = scan.Value.NearestEnemy(selfId, selfShip.transform.position, registry);
            if (enemyId.IsValid && registry.TryGetShip(enemyId, out var enemy))
                cachedEnemy = enemy;
        }
    }
}

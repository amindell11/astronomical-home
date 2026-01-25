using AI.Computers;
using Ships;
using UnityEngine;

namespace AI.Context
{
    public class Combat
    {
        private readonly Scanning.Scout scout;
        private readonly Gunner gunner;
        private readonly Targeting targeting;
        private readonly ShipId selfId;
        private readonly IShipRegistry registry;

        private ShipId enemyId;
        private Ship cachedEnemy;

        public Combat(Scanning.Scout scout, Gunner gunner, Targeting targeting)
        {
            this.scout = scout;
            this.gunner = gunner;
            this.targeting = targeting;
            this.selfId = scout.ShipId;
            this.registry = scout.Registry;
        }

        public bool InCombat => cachedEnemy && cachedEnemy.gameObject.activeInHierarchy;

        public Ship Enemy
        {
            get
            {
                if (InCombat) return cachedEnemy;

                if (registry == null || !registry.TryGetShip(selfId, out var selfShip)) return null;

                var scan = scout.ShipScan;
                if (scan == null) return null;

                enemyId = scan.Value.NearestEnemy(selfId, selfShip.transform.position, registry);
                if (enemyId.IsValid && registry.TryGetShip(enemyId, out cachedEnemy))
                    return cachedEnemy;

                cachedEnemy = null;
                return null;
            }
        }

        public Vector2 EnemyPos => Enemy?.CurrentState.kinematics.pos ?? Vector2.zero;
        public Vector2 EnemyVel => Enemy?.CurrentState.kinematics.vel ?? Vector2.zero;
        public Vector2 EnemyForward => Enemy?.CurrentState.kinematics.Forward ?? Vector2.up;
        public float EnemyHealthPct => Enemy?.CurrentState.healthPct ?? 0f;
        public float EnemyShieldPct => Enemy?.CurrentState.shieldPct ?? 0f;

        public Vector2 VectorToTarget => gunner?.VectorToTarget ?? Vector2.zero;
        public bool HasTargetLos => gunner?.HasTargetLos ?? false;
        public float AngleToTarget => gunner?.AngleToTarget ?? 0f;

        public bool IncomingMissile => false; //TODO
        public float LaserSpeed => 0f; //TODO
    }
}

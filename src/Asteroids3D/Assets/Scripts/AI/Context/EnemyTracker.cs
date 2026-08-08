using Ships;
using UnityEngine;

namespace AI.Context
{
    /// <summary>
    /// Tracks the AI's current enemy: acquisition via the <see cref="Scanning.Scout"/>, the
    /// enemy's live kinematics/durability, and a combat-exit grace period. The "enemy" half of
    /// the world model — distinct from the <c>Combat</c> package's weapon mechanics.
    /// </summary>
    public class EnemyTracker
    {
        private readonly Scout scout;
        private readonly float combatExitDelay;

        private Ship cachedEnemy;
        private float simTime;
        private float lastContactTime = -1f;

        public EnemyTracker(Scout scout, float combatExitDelay)
        {
            this.scout = scout;
            this.combatExitDelay = combatExitDelay;
        }

        public Ship Enemy => cachedEnemy;
        public bool HasEnemy => cachedEnemy && cachedEnemy.gameObject.activeInHierarchy;
        public bool InCombat => HasEnemy || TimeSinceCombat < combatExitDelay;
        public float TimeSinceCombat { get; private set; } = float.MaxValue;

        public Vector2 EnemyPos => HasEnemy ? cachedEnemy.Kinematics.pos : Vector2.zero;
        public Vector2 EnemyVel => HasEnemy ? cachedEnemy.Kinematics.vel : Vector2.zero;
        public Movement.Dynamics EnemyDynamics => HasEnemy ? cachedEnemy.Dynamics : default;
        public Vector2 EnemyForward => HasEnemy ? cachedEnemy.Kinematics.Forward : Vector2.up;
        public float EnemyYawRate => HasEnemy ? cachedEnemy.Kinematics.yawRate : 0f;
        public float EnemyHealthPct => HasEnemy ? cachedEnemy.HealthPct : 0f;
        public float EnemyShieldPct => HasEnemy ? cachedEnemy.ShieldPct : 0f;

        public bool IncomingMissile => false; // TODO

        public void Update(float deltaTime)
        {
            simTime += deltaTime;

            if (cachedEnemy == null || !cachedEnemy.gameObject.activeInHierarchy)
            {
                cachedEnemy = null;
                AcquireEnemy();
            }

            if (HasEnemy)
            {
                lastContactTime = simTime;
                TimeSinceCombat = 0f;
            }
            else
            {
                TimeSinceCombat = lastContactTime >= 0f ? simTime - lastContactTime : float.MaxValue;
            }
        }

        private void AcquireEnemy()
        {
            var registry = scout.Registry;
            var enemyId = scout.Contacts.nearestEnemyId;
            if (enemyId.IsValid && registry != null && registry.TryGetShip(enemyId, out var enemy))
                cachedEnemy = enemy;
        }
    }
}

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
        private readonly Scanning.Scout scout;
        private readonly float combatExitDelay;

        private Ship cachedEnemy;
        private float lastContactTime = -1f;

        public EnemyTracker(Scanning.Scout scout, float combatExitDelay)
        {
            this.scout = scout;
            this.combatExitDelay = combatExitDelay;
        }

        public Ship Enemy => cachedEnemy;
        public bool HasEnemy => cachedEnemy && cachedEnemy.gameObject.activeInHierarchy;
        public bool InCombat => HasEnemy || TimeSinceCombat < combatExitDelay;
        public float TimeSinceCombat { get; private set; } = float.MaxValue;

        public Vector2 EnemyPos => HasEnemy ? cachedEnemy.CurrentState.kinematics.pos : Vector2.zero;
        public Vector2 EnemyVel => HasEnemy ? cachedEnemy.CurrentState.kinematics.vel : Vector2.zero;
        public Movement.Dynamics EnemyDynamics => HasEnemy ? cachedEnemy.Dynamics : default;
        public Vector2 EnemyForward => HasEnemy ? cachedEnemy.CurrentState.kinematics.Forward : Vector2.up;
        public float EnemyYawRate => HasEnemy ? cachedEnemy.CurrentState.kinematics.yawRate : 0f;
        public float EnemyHealthPct => HasEnemy ? cachedEnemy.CurrentState.healthPct : 0f;
        public float EnemyShieldPct => HasEnemy ? cachedEnemy.CurrentState.shieldPct : 0f;

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
            var registry = scout.Registry;
            var enemyId = scout.Contacts.nearestEnemyId;
            if (enemyId.IsValid && registry != null && registry.TryGetShip(enemyId, out var enemy))
                cachedEnemy = enemy;
        }
    }
}

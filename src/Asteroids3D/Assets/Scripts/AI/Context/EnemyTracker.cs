using Ships;
using Substrate;
using UnityEngine;
using Utils;

namespace AI.Context
{
    /// <summary>
    /// Tracks the AI's current enemy: acquisition via the <see cref="Scout"/>, the enemy's live
    /// kinematics, and a combat-exit grace period. Unlimited, an acquired enemy is kept at any
    /// range. Sight-limited, the enemy is held only while it is seen, and a lost one can be handed
    /// back. The "enemy" half of the world model, distinct from the <c>Combat</c> package's weapon
    /// mechanics.
    /// </summary>
    public class EnemyTracker
    {
        private readonly Scout scout;
        private readonly float combatExitDelay;

        private Ship cachedEnemy;
        private Ship lostEnemy;
        private float simTime;
        private float lastContactTime = -1f;
        private float lastSeenTime;

        public EnemyTracker(Scout scout, float combatExitDelay)
        {
            this.scout = scout;
            this.combatExitDelay = combatExitDelay;
        }

        public Ship Enemy => cachedEnemy;
        public bool HasEnemy => cachedEnemy && cachedEnemy.gameObject.activeInHierarchy && !cachedEnemy.Lock.IsConcealed;
        public bool InCombat => HasEnemy || TimeSinceCombat < combatExitDelay;
        public float TimeSinceCombat { get; private set; } = float.MaxValue;
        internal bool SightLimited { get; private set; }

        public Vector2 EnemyPos => HasEnemy ? cachedEnemy.Kinematics.pos : Vector2.zero;
        public Vector2 EnemyVel => HasEnemy ? cachedEnemy.Kinematics.vel : Vector2.zero;
        public Vector2 EnemyForward => HasEnemy ? cachedEnemy.Kinematics.Forward : Vector2.up;
        public float EnemyHealthPct => HasEnemy ? cachedEnemy.HealthPct : 0f;
        public float EnemyShieldPct => HasEnemy ? cachedEnemy.ShieldPct : 0f;

        internal void LimitToSight(Ship known)
        {
            SightLimited = true;
            cachedEnemy = known;
            lostEnemy = null;
            lastSeenTime = simTime;
        }

        internal void Recall()
        {
            if (Alive(lostEnemy))
            {
                cachedEnemy = lostEnemy;
                lastSeenTime = simTime;
            }

            lostEnemy = null;
        }

        public void Update(float deltaTime)
        {
            simTime += deltaTime;

            if (SightLimited)
                UpdateSight();
            else if (!HasEnemy)
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

        private void UpdateSight()
        {
            if (cachedEnemy && !cachedEnemy.gameObject.activeInHierarchy)
                cachedEnemy = null;
            else if (cachedEnemy && cachedEnemy.Lock.IsConcealed)
            {
                // A concealed enemy is lost, not dead, so the search can recall it once it shows.
                lostEnemy = cachedEnemy;
                cachedEnemy = null;
            }
            else if (cachedEnemy)
            {
                if (Sees(cachedEnemy))
                    lastSeenTime = simTime;
                else if (simTime - lastSeenTime >= scout.loseSightAfter)
                {
                    lostEnemy = cachedEnemy;
                    cachedEnemy = null;
                }
            }

            if (!HasEnemy)
                AcquireSeen();
        }

        private void AcquireEnemy()
        {
            var registry = scout.Registry;
            var enemyId = scout.Contacts.nearestEnemyId;
            if (enemyId.IsValid && registry != null && registry.TryGetShip(enemyId, out var enemy))
                cachedEnemy = enemy;
        }

        private void AcquireSeen()
        {
            var registry = scout.Registry;
            var enemyId = scout.Contacts.nearestEnemyId;
            if (!enemyId.IsValid || registry == null || !registry.TryGetShip(enemyId, out var enemy) || !Sees(enemy))
                return;
            cachedEnemy = enemy;
            lastSeenTime = simTime;
        }

        private bool Sees(Ship enemy)
        {
            var from = scout.Origin;
            var to = enemy.transform.position;
            var separation = GamePlane.WorldPointToPlane(to) - GamePlane.WorldPointToPlane(from);
            if (separation.magnitude > scout.sightRange) return false;
            return LineOfSight.IsClear(from, to, enemy.transform, scout.asteroidMask);
        }

        private static bool Alive(Ship ship) =>
            ship && ship.gameObject.activeInHierarchy && !ship.Lock.IsConcealed;
    }
}

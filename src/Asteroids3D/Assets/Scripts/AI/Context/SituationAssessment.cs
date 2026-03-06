using UnityEngine;
using TargetingUtils = Combat.TargetingUtils;

namespace AI.Context
{
    public readonly struct SituationAssessment
    {
        // Combat status
        public readonly bool InCombat;
        public readonly float TimeSinceCombat;

        // Self
        public readonly float HealthPct;
        public readonly float ShieldPct;
        public readonly float SpeedPct;
        public readonly float CombinedDurability; // (health + shield) / 2

        // Enemy (0/default when no enemy)
        public readonly float EnemyDistance;
        public readonly float EnemyCombinedDurability; // (enemyHealth + enemyShield) / 2

        // Threat (normalized 0-1)
        public readonly float Outnumbered; // Clamp01((enemies - friends) / 3)
        public readonly int NearbyEnemyCount;
        public readonly int NearbyFriendCount;

        // Spatial
        public readonly bool HasLineOfSight;
        public readonly float ClosingRate;       // Clamp01(raw * 0.05 + 0.5) -- 0=retreating, 0.5=static, 1=closing fast
        public readonly float EnemyFacingThreat; // (cos(angle) + 1) / 2 -- 0=facing away, 1=facing directly at us
        public readonly float SelfAngleToEnemy;  // raw degrees 0-180
        public readonly float SelfAngleNorm;     // angle / 180

        // Environment
        public readonly bool NearCover;
        public readonly bool IncomingMissile;

        private SituationAssessment(
            bool inCombat, float timeSinceCombat,
            float healthPct, float shieldPct, float speedPct, float combinedDurability,
            float enemyDistance, float enemyCombinedDurability,
            float outnumbered, int nearbyEnemyCount, int nearbyFriendCount,
            bool hasLineOfSight, float closingRate, float enemyFacingThreat,
            float selfAngleToEnemy, float selfAngleNorm,
            bool nearCover, bool incomingMissile)
        {
            InCombat = inCombat;
            TimeSinceCombat = timeSinceCombat;
            HealthPct = healthPct;
            ShieldPct = shieldPct;
            SpeedPct = speedPct;
            CombinedDurability = combinedDurability;
            EnemyDistance = enemyDistance;
            EnemyCombinedDurability = enemyCombinedDurability;
            Outnumbered = outnumbered;
            NearbyEnemyCount = nearbyEnemyCount;
            NearbyFriendCount = nearbyFriendCount;
            HasLineOfSight = hasLineOfSight;
            ClosingRate = closingRate;
            EnemyFacingThreat = enemyFacingThreat;
            SelfAngleToEnemy = selfAngleToEnemy;
            SelfAngleNorm = selfAngleNorm;
            NearCover = nearCover;
            IncomingMissile = incomingMissile;
        }

        public static readonly SituationAssessment None = new SituationAssessment(
            inCombat: false, timeSinceCombat: float.MaxValue,
            healthPct: 1f, shieldPct: 1f, speedPct: 0f, combinedDurability: 1f,
            enemyDistance: float.MaxValue, enemyCombinedDurability: 0f,
            outnumbered: 0f, nearbyEnemyCount: 0, nearbyFriendCount: 0,
            hasLineOfSight: false, closingRate: 0.5f, enemyFacingThreat: 0f,
            selfAngleToEnemy: 180f, selfAngleNorm: 1f,
            nearCover: false, incomingMissile: false);

        public static SituationAssessment Evaluate(
            ShipInfo shipInfo,
            CombatTracker combat,
            Scanning.Scout scout,
            TargetingUtils targeting)
        {
            var healthPct = shipInfo.HealthPct;
            var shieldPct = shipInfo.ShieldPct;
            var speedPct = shipInfo.SpeedPct;
            var combinedDurability = (healthPct + shieldPct) / 2f;

            var scan = scout.ShipScan;
            var enemyCount = scan?.EnemyCount(scout.ShipId, scout.Registry) ?? 0;
            var friendCount = scan?.FriendCount(scout.ShipId, scout.Registry) ?? 0;
            var outnumbered = Mathf.Clamp01((enemyCount - friendCount) / 3f);

            var hasEnemy = combat.HasEnemy;
            var enemyDistance = float.MaxValue;
            var enemyCombinedDurability = 0f;
            var hasLos = false;
            var closingRate = 0.5f;
            var enemyFacingThreat = 0f;
            var selfAngle = 180f;

            if (hasEnemy)
            {
                var enemyPos = combat.EnemyPos;
                var vec = enemyPos - shipInfo.Pos;
                enemyDistance = vec.magnitude;
                enemyCombinedDurability = (combat.EnemyHealthPct + combat.EnemyShieldPct) / 2f;

                hasLos = targeting.HasLineOfSight(
                    new Vector3(enemyPos.x, enemyPos.y, shipInfo.Pos3D.z));

                var rawClosing = targeting.ClosingSpeed(enemyPos, combat.EnemyVel);
                closingRate = Mathf.Clamp01(rawClosing * 0.05f + 0.5f);

                var angleFromEnemy = targeting.AngleFromTarget(enemyPos, combat.EnemyForward);
                enemyFacingThreat = (Mathf.Cos(angleFromEnemy * Mathf.Deg2Rad) + 1f) / 2f;

                selfAngle = targeting.AngleTo(enemyPos);
            }

            return new SituationAssessment(
                inCombat: combat.InCombat,
                timeSinceCombat: combat.TimeSinceCombat,
                healthPct: healthPct,
                shieldPct: shieldPct,
                speedPct: speedPct,
                combinedDurability: combinedDurability,
                enemyDistance: enemyDistance,
                enemyCombinedDurability: enemyCombinedDurability,
                outnumbered: outnumbered,
                nearbyEnemyCount: enemyCount,
                nearbyFriendCount: friendCount,
                hasLineOfSight: hasLos,
                closingRate: closingRate,
                enemyFacingThreat: enemyFacingThreat,
                selfAngleToEnemy: selfAngle,
                selfAngleNorm: selfAngle / 180f,
                nearCover: scout.HasNearbyCover,
                incomingMissile: combat.IncomingMissile);
        }

        public override string ToString()
        {
            return $"Assessment[HP:{HealthPct:F2} Shield:{ShieldPct:F2} " +
                   $"EnemyDist:{EnemyDistance:F1} LOS:{HasLineOfSight} " +
                   $"Combat:{InCombat} Enemies:{NearbyEnemyCount} Friends:{NearbyFriendCount}]";
        }
    }
}

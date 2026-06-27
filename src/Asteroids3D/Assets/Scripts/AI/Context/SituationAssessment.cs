using Game;
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
            SelfStatus self = null,
            CombatStatus combat = null,
            Scanning.Scout scout = null,
            TargetingUtils targeting = null)
        {
            InCombat = combat?.InCombat ?? false;
            TimeSinceCombat = combat?.TimeSinceCombat ?? float.MaxValue;
            HealthPct = self?.HealthPct ?? 1f;
            ShieldPct = self?.ShieldPct ?? 1f;
            SpeedPct = self?.SpeedPct ?? 0f;
            CombinedDurability = (HealthPct + ShieldPct) / 2f;
            EnemyDistance = float.MaxValue;
            EnemyCombinedDurability = 0f;
            Outnumbered = 0f;
            NearbyEnemyCount = 0;
            NearbyFriendCount = 0;
            HasLineOfSight = false;
            ClosingRate = 0.5f;
            EnemyFacingThreat = 0f;
            SelfAngleToEnemy = 180f;
            SelfAngleNorm = 1f;
            NearCover = scout?.HasNearbyCover ?? false;
            IncomingMissile = combat?.IncomingMissile ?? false;

            if (scout) {
                var contacts = scout.Contacts;
                NearbyEnemyCount = contacts.EnemyCount;
                NearbyFriendCount = contacts.FriendCount;
                Outnumbered = contacts.Outnumbered;
            }

            if (self == null || combat == null || targeting == null || !combat.HasEnemy)
                return;

            var enemyPos = combat.EnemyPos;
            EnemyDistance = (enemyPos - self.Pos).magnitude;
            EnemyCombinedDurability = (combat.EnemyHealthPct + combat.EnemyShieldPct) / 2f;
            HasLineOfSight = targeting.HasLineOfSight(GamePlane.PlanePointToWorld(enemyPos));

            var rawClosing = targeting.ClosingSpeed(enemyPos, combat.EnemyVel);
            ClosingRate = Mathf.Clamp01(rawClosing * 0.05f + 0.5f);

            var angleFromEnemy = targeting.AngleFromTarget(enemyPos, combat.EnemyForward);
            EnemyFacingThreat = (Mathf.Cos(angleFromEnemy * Mathf.Deg2Rad) + 1f) / 2f;

            SelfAngleToEnemy = targeting.AngleTo(enemyPos);
            SelfAngleNorm = SelfAngleToEnemy / 180f;
        }

        public static readonly SituationAssessment None = new();

        public static SituationAssessment Evaluate(
            SelfStatus self,
            CombatStatus combat,
            Scanning.Scout scout,
            TargetingUtils targeting)
        {
            return new SituationAssessment(self, combat, scout, targeting);
        }

        public override string ToString()
        {
            return $"Assessment[HP:{HealthPct:F2} Shield:{ShieldPct:F2} " +
                   $"EnemyDist:{EnemyDistance:F1} LOS:{HasLineOfSight} " +
                   $"Combat:{InCombat} Enemies:{NearbyEnemyCount} Friends:{NearbyFriendCount}]";
        }

    }
}

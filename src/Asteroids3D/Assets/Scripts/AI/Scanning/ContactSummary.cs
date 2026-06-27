using Ships;
using UnityEngine;

namespace AI.Scanning
{
    /// <summary>
    /// Per-tick summary of the ship scan: who the nearest enemy is and how
    /// the local force balance looks. Built once by <see cref="Scout"/> so
    /// consumers (enemy tracking, situation assessment) read cached counts
    /// instead of each re-walking the raw scan.
    /// </summary>
    public readonly struct ContactSummary
    {
        public readonly ShipId NearestEnemyId;
        public readonly int EnemyCount;
        public readonly int FriendCount;
        /// <summary>Clamp01((enemies - friends) / 3): 0 = even or outnumbering, 1 = heavily outnumbered.</summary>
        public readonly float Outnumbered;

        public ContactSummary(ShipId nearestEnemyId, int enemyCount, int friendCount)
        {
            NearestEnemyId = nearestEnemyId;
            EnemyCount = enemyCount;
            FriendCount = friendCount;
            Outnumbered = Mathf.Clamp01((float)(enemyCount - friendCount) / 3f);
        }

        public static readonly ContactSummary Empty = new(ShipId.Invalid, 0, 0);

        public static ContactSummary Build(in ShipScanResult scan, ShipId self, Vector3 selfPos, IShipRegistry registry)
        {
            if (registry == null) return Empty;
            return new ContactSummary(
                scan.NearestEnemy(self, selfPos, registry),
                scan.EnemyCount(self, registry),
                scan.FriendCount(self, registry));
        }
    }
}

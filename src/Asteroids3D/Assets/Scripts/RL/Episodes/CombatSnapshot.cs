using System;
using Ships;
using UnityEngine;

namespace RL.Episodes
{
    /// <summary>One agent-centric read of the 1v1 state; the episode rules are pure functions over these.</summary>
    [Serializable]
    public struct CombatSnapshot
    {
        public float myPool;
        public float enemyPool;
        public bool myAlive;
        public bool enemyAlive;
        public float myDistFromCenter;
        public float enemyDistFromCenter;
    }

    /// <summary>The only episode-layer piece that touches live ships: reads pools, liveness and distance from the arena center into a <see cref="CombatSnapshot"/>.</summary>
    public static class CombatSnapshotExtractor
    {
        public static CombatSnapshot Capture(Ship self, Ship enemy, Vector2 arenaCenter)
        {
            var myKin = self.Kinematics;
            var enemyKin = enemy.Kinematics;
            return new CombatSnapshot
            {
                myPool = Pool(self),
                enemyPool = Pool(enemy),
                myAlive = Alive(self),
                enemyAlive = Alive(enemy),
                myDistFromCenter = (myKin.pos - arenaCenter).magnitude,
                enemyDistFromCenter = (enemyKin.pos - arenaCenter).magnitude,
            };
        }

        private static float Pool(Ship ship) =>
            ship.Damage.Health.CurrentValue + ship.Damage.Shield.CurrentValue;

        private static bool Alive(Ship ship) => ship.Damage.Health.CurrentValue > 0f;
    }
}

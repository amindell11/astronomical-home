using System;
using AI;
using Ships;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>One agent-centric observation of the 1v1 state; the reward layer is pure functions over pairs of these.</summary>
    [Serializable]
    public struct CombatSnapshot
    {
        public float myPool;
        public float myMaxPool;
        public float enemyPool;
        public float enemyMaxPool;
        public Vector2 myPos;
        public Vector2 myVel;
        public Vector2 enemyPos;
        public Vector2 enemyVel;
        public bool myAlive;
        public bool enemyAlive;
        public bool inMyEnvelope;
        public bool inEnemyEnvelope;
        public float myDistFromCenter;
        public float enemyDistFromCenter;
        public float distanceToTarget;
        // Primary weapon's envelope max range — the pursuit-ramp saturation radius, read here (the only live-ship touch) so the reward math never reaches into the weapon.
        public float fireDistance;
    }

    /// <summary>The only reward-layer piece that touches live ships: reads pools, kinematics, and geometric weapon envelopes into a <see cref="CombatSnapshot"/>.</summary>
    public static class CombatSnapshotExtractor
    {
        public static CombatSnapshot Capture(Ship self, Ship enemy, Vector2 arenaCenter)
        {
            var myKin = self.Kinematics;
            var enemyKin = enemy.Kinematics;
            return new CombatSnapshot
            {
                myPool = Pool(self),
                myMaxPool = MaxPool(self),
                enemyPool = Pool(enemy),
                enemyMaxPool = MaxPool(enemy),
                myPos = myKin.pos,
                myVel = myKin.vel,
                enemyPos = enemyKin.pos,
                enemyVel = enemyKin.vel,
                myAlive = Alive(self),
                enemyAlive = Alive(enemy),
                inMyEnvelope = AnySlotInEnvelope(self, enemy),
                inEnemyEnvelope = AnySlotInEnvelope(enemy, self),
                myDistFromCenter = (myKin.pos - arenaCenter).magnitude,
                enemyDistFromCenter = (enemyKin.pos - arenaCenter).magnitude,
                distanceToTarget = (enemyKin.pos - myKin.pos).magnitude,
                fireDistance = PrimaryFireRange(self),
            };
        }

        private static float PrimaryFireRange(Ship ship)
        {
            var primary = ship.Weapons ? ship.Weapons.Primary : null;
            return primary ? primary.FireRange : 0f;
        }

        private static float Pool(Ship ship) =>
            ship.Damage.Health.CurrentValue + ship.Damage.Shield.CurrentValue;

        private static float MaxPool(Ship ship) =>
            ship.Damage.Health.MaxValue + ship.Damage.Shield.MaxValue;

        private static bool Alive(Ship ship) => ship.Damage.Health.CurrentValue > 0f;

        private static bool AnySlotInEnvelope(Ship shooter, Ship target)
        {
            var weapons = shooter.Weapons ? shooter.Weapons.Context : null;
            if (weapons == null || !Alive(shooter) || !Alive(target)) return false;

            var shooterKin = shooter.Kinematics;
            var targetKin = target.Kinematics;
            var slots = weapons.Slots;
            for (var i = 0; i < slots.Count; i++)
            {
                // Evaluate at the same per-slot intercept lead the gunner fires at, not the target's center.
                var aimWorld = GamePlane.PlanePointToWorld(Gunner.AimPoint(
                    in shooterKin, targetKin.pos, targetKin.vel, weapons.ProjectileSpeed(slots[i])));
                if (weapons.Sight(slots[i])?.InEnvelope(aimWorld) ?? false)
                    return true;
            }
            return false;
        }
    }
}

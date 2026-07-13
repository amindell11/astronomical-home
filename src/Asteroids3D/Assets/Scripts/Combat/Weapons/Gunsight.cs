using System;
using Combat.Weapons;
using Game;
using Movement;
using UnityEngine;

namespace Combat
{
    /// <summary>Builds the <see cref="TargetingContext"/> for one weapon against a world-space target point and defers the fire/envelope policy to the weapon — target-supplier-agnostic.</summary>
    public sealed class Gunsight
    {
        private readonly WeaponComponent weapon;
        private readonly Func<Kinematics> pose;
        private readonly LosCache los;

        public Gunsight(WeaponComponent weapon, Func<Kinematics> pose)
        {
            this.weapon = weapon;
            this.pose = pose;
            los = new LosCache();
        }

        public Vector3 FirePoint => (weapon && weapon.firePoint)
            ? weapon.firePoint.position
            : (weapon ? weapon.transform.position : Vector3.zero);

        /// <summary>Returns whether the weapon should fire at the given world-space target this tick.</summary>
        public bool Evaluate(Vector3 targetWorld)
        {
            return weapon && weapon.ShouldFire(BuildContext(targetWorld));
        }

        /// <summary>Whether the target sits in the weapon's geometric envelope (readiness excluded).</summary>
        public bool InEnvelope(Vector3 targetWorld)
        {
            if (!weapon) return false;
            var context = BuildContext(targetWorld);
            return weapon.InEnvelope(in context);
        }

        private TargetingContext BuildContext(Vector3 targetWorld)
        {
            var k = pose();
            var targetPlane = GamePlane.WorldPointToPlane(targetWorld);
            var angle = TargetingMath.AngleTo(k, targetPlane);

            return new TargetingContext
            {
                targetPosition = targetPlane,
                distanceToTarget = TargetingMath.DistanceTo(k, targetPlane),
                angleToTarget = angle,
                hasLineOfSight = los.IsClear(FirePoint, targetWorld, angle),
            };
        }
    }
}

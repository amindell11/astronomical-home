using System;
using Combat.Weapons;
using Game;
using Movement;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Drives a single weapon's fire decision against a world-space target point.
    /// Builds the <see cref="TargetingContext"/> from shared aiming geometry and defers
    /// the actual "should I fire" policy to the weapon (<see cref="WeaponComponent.ShouldFire"/>).
    ///
    /// This is the firing half of targeting, decoupled from <em>who</em> supplies the target:
    /// the AI <c>Gunner</c> feeds it an intercept point, while a static laser turret would feed
    /// it a point from its own sensor. Neither needs to know the other exists.
    /// </summary>
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
            if (!weapon) return false;

            var k = pose();
            var targetPlane = GamePlane.WorldPointToPlane(targetWorld);
            var angle = TargetingMath.AngleTo(k, targetPlane);

            var context = new TargetingContext
            {
                targetPosition = targetPlane,
                distanceToTarget = TargetingMath.DistanceTo(k, targetPlane),
                angleToTarget = angle,
                hasLineOfSight = los.IsClear(FirePoint, targetWorld, angle),
            };

            return weapon.ShouldFire(context);
        }
    }
}

using System;
using Combat.Weapons;
using Game;
using Movement;
using UnityEngine;
using Combat.Targeting;

namespace Combat.Weapons
{
    /// <summary>Builds the <see cref="TargetingContext"/> for one weapon against a world-space target point and defers the fire/envelope policy to the weapon — target-supplier-agnostic.</summary>
    public sealed class Gunsight
    {
        private readonly WeaponComponent weapon;
        private readonly Func<Kinematics> pose;
        private readonly LosCache los;
        // Observation queries get their own cache so reward/telemetry reads never mutate the firing path's LOS state.
        private readonly LosCache observationLos;

        public Gunsight(WeaponComponent weapon, Func<Kinematics> pose)
        {
            this.weapon = weapon;
            this.pose = pose;
            los = new LosCache();
            observationLos = new LosCache();
        }

        public Vector3 FirePoint => (weapon && weapon.firePoint)
            ? weapon.firePoint.position
            : (weapon ? weapon.transform.position : Vector3.zero);

        /// <summary>Returns whether the weapon should fire at the given world-space target this tick.</summary>
        public bool Evaluate(Vector3 targetWorld)
        {
            return weapon && weapon.ShouldFire(BuildContext(targetWorld, los));
        }

        /// <summary>Whether the target sits in the weapon's geometric envelope (readiness excluded); read-only w.r.t. the firing path.</summary>
        public bool InEnvelope(Vector3 targetWorld)
        {
            if (!weapon) return false;
            var context = BuildContext(targetWorld, observationLos);
            return weapon.InEnvelope(in context);
        }

        private TargetingContext BuildContext(Vector3 targetWorld, LosCache losCache)
        {
            var k = pose();
            var targetPlane = GamePlane.WorldPointToPlane(targetWorld);
            var angle = TargetingMath.AngleTo(k, targetPlane);

            return new TargetingContext
            {
                targetPosition = targetPlane,
                distanceToTarget = TargetingMath.DistanceTo(k, targetPlane),
                angleToTarget = angle,
                hasLineOfSight = losCache.IsClear(FirePoint, targetWorld, angle),
            };
        }
    }
}

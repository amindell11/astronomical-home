using System;
using AI.Context;
using AI.Gunnery;
using AI.Navigation.MPC;
using Combat;
using Combat.Weapons;
using Movement;
using Ships.Command;
using UnityEngine;
using Combat.Targeting;
using Substrate;
using Utils;

namespace AI
{
    [DefaultExecutionOrder(-50)]
    public class Gunner : MonoBehaviour
    {
        internal IWeaponContext weapons;
        private IWeapons actuator;
        private Func<Kinematics> pose;
        private AimPerception perception;

        private Vector2 targetPos;
        private Vector2 targetVel;
        private bool hasTarget;
        private bool prevPrimaryHeld;
        private bool prevSecondaryHeld;

        /// <summary>The primary weapon's intercept point (world space), for diagnostics/gizmos.</summary>
        internal Vector3 Target { get; private set; }
        public bool HasTarget => hasTarget;

        /// <summary>Muzzle speed of the primary weapon, used by the navigator for intercept lead.</summary>
        public float PrimaryProjectileSpeed => weapons?.ProjectileSpeed(WeaponSlot.Primary) ?? 0f;
        private void ClearTarget() => Target = Vector3.zero;

        public void Initialize(IWeaponContext weapons, IWeapons actuator, Func<Kinematics> poseFunc,
            AimSettings aimSettings, SeedScope aimScope)
        {
            if (!aimSettings)
                throw new ArgumentNullException(nameof(aimSettings),
                    $"{name}: an armed AI pilot needs AimSettings, assigned on its AICommander.");
            this.weapons = weapons;
            this.actuator = actuator;
            pose = poseFunc;
            perception = new AimPerception(aimSettings, aimScope);
        }

        /// <summary>Drops the tracked target and aim point, restoring the freshly-initialized gunner.</summary>
        public void ResetState()
        {
            hasTarget = false;
            targetPos = default;
            targetVel = default;
            prevPrimaryHeld = false;
            prevSecondaryHeld = false;
            perception?.Reset();
            ClearTarget();
        }

        /// <summary>Pushes each slot's raw trigger every step; pressed marks only the step held rises.</summary>
        public void Fire(bool engagePrimary, bool engageSecondary)
        {
            if (weapons == null || actuator == null) return;

            var slots = weapons.Slots;
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var engage = slot == WeaponSlot.Primary ? engagePrimary : engageSecondary;
                var aim = AimPointFor(slot);
                var held = engage && hasTarget && (weapons.Sight(slot)?.Evaluate(aim) ?? false);
                if (slot == WeaponSlot.Primary) FireSlot(slot, held, aim, ref prevPrimaryHeld);
                else FireSlot(slot, held, aim, ref prevSecondaryHeld);
            }
        }

        private void FireSlot(WeaponSlot slot, bool held, Vector3 aim, ref bool prevHeld)
        {
            var cmd = new WeaponCommand { held = held, pressed = held && !prevHeld, targetPoint = aim };
            prevHeld = held;
            actuator.Fire(slot, cmd);
        }

        /// <summary>The gunner's aim policy for one weapon: intercept lead from its muzzle speed; non-positive speed = hitscan, aim at the present position.</summary>
        public static Vector2 AimPoint(in Kinematics shooterPose, Vector2 targetPos, Vector2 targetVel, float projectileSpeed) =>
            projectileSpeed <= 0f
                ? targetPos
                : TargetingMath.PredictIntercept(in shooterPose, targetPos, targetVel, projectileSpeed);

        /// <summary>Bearing to the primary weapon's aim point and its rate; none without a target.</summary>
        public AimReference PrimaryAimReference()
        {
            if (!hasTarget) return AimReference.None;

            var self = pose();
            var toAim = AimPoint(in self, targetPos, targetVel, weapons.ProjectileSpeed(WeaponSlot.Primary)) - self.pos;
            var rangeSq = toAim.sqrMagnitude;
            // On top of the aim point the bearing is undefined.
            if (rangeSq < 0.01f) return AimReference.None;

            var relVel = targetVel - self.vel;
            return new AimReference(Mathf.Atan2(-toAim.x, toAim.y),
                (toAim.x * relVel.y - toAim.y * relVel.x) / rangeSq);
        }

        internal Vector3 AimPointFor(WeaponSlot slot)
        {
            if (pose == null)
                return GamePlane.PlanePointToWorld(targetPos);

            return GamePlane.PlanePointToWorld(
                AimPoint(pose(), targetPos, targetVel, weapons.ProjectileSpeed(slot)));
        }

        /// <summary>Stores the fire lane's anchor as perceived, for per-slot firing solutions and the aim reference.</summary>
        public void Aim(in EnemyTarget anchor)
        {
            if (pose == null) return;

            perception.Perceive(pose(), in anchor, Time.fixedTimeAsDouble, out targetPos, out targetVel);
            hasTarget = true;
            Target = AimPointFor(WeaponSlot.Primary);
        }

        /// <summary>The AIM-referent swap's entry: a rock gets the same perception and lead as a ship.</summary>
        public void Aim(Vector2 referentPos, Vector2 referentVel) =>
            Aim(new EnemyTarget { kinematics = new Kinematics(referentPos, referentVel, 0f, 0f, 0f) });

        /// <summary>Drops the aim without dropping the weapons: the slots stay the gunner's, it just has nothing to shoot at.</summary>
        public void HoldFire()
        {
            hasTarget = false;
            ClearTarget();
        }
    }
}

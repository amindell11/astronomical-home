using System;
using AI.Context;
using Combat;
using Game;
using Movement;
using Ships.Command;
using UnityEngine;

namespace AI
{
    [DefaultExecutionOrder(-50)]
    public class Gunner : MonoBehaviour
    {
        internal IWeaponContext weapons;
        private IWeapons actuator;
        private Func<Kinematics> pose;

        private Vector2 targetPos;
        private Vector2 targetVel;
        private bool hasTarget;

        /// <summary>The primary weapon's intercept point (world space), for diagnostics/gizmos.</summary>
        internal Vector3 Target { get; private set; }
        public bool HasTarget => hasTarget;

        /// <summary>Muzzle speed of the primary weapon, used by the navigator for intercept lead.</summary>
        public float PrimaryProjectileSpeed => weapons?.ProjectileSpeed(WeaponSlot.Primary) ?? 0f;
        private void ClearTarget() => Target = Vector3.zero;

        public void Initialize(IWeaponContext weapons, IWeapons actuator, Func<Kinematics> poseFunc)
        {
            this.weapons = weapons;
            this.actuator = actuator;
            pose = poseFunc;
        }

        /// <summary>Drops the tracked target and aim point, restoring the freshly-initialized gunner.</summary>
        public void ResetState()
        {
            hasTarget = false;
            targetPos = default;
            targetVel = default;
            ClearTarget();
        }

        /// <summary>Evaluates each engaged slot with that slot's own ballistics and pushes press+hold each step it wants fire; the weapons' own trigger semantics pace the shots. The brain gates, the gunner times: a disengaged slot pushes a released trigger.</summary>
        public void Fire(bool engagePrimary, bool engageSecondary)
        {
            if (weapons == null || actuator == null) return;

            var slots = weapons.Slots;
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var engage = slot == WeaponSlot.Primary ? engagePrimary : engageSecondary;
                var fire = engage && hasTarget && (weapons.Sight(slot)?.Evaluate(AimPointFor(slot)) ?? false);
                actuator.Fire(slot, new WeaponCommand { held = fire, pressed = fire });
            }
        }

        /// <summary>The gunner's aim policy for one weapon: intercept lead from its muzzle speed; non-positive speed = hitscan, aim at the present position.</summary>
        public static Vector2 AimPoint(in Kinematics shooterPose, Vector2 targetPos, Vector2 targetVel, float projectileSpeed) =>
            projectileSpeed <= 0f
                ? targetPos
                : TargetingMath.PredictIntercept(in shooterPose, targetPos, targetVel, projectileSpeed);

        internal Vector3 AimPointFor(WeaponSlot slot)
        {
            if (pose == null)
                return GamePlane.PlanePointToWorld(targetPos);

            return GamePlane.PlanePointToWorld(
                AimPoint(pose(), targetPos, targetVel, weapons.ProjectileSpeed(slot)));
        }

        /// <summary>Consumes the fire lane's anchor (mirrors <c>Navigator.ApplyObjective</c>): stores enemy kinematics for per-slot firing solutions.</summary>
        public void Aim(in EnemyTarget anchor) => Aim(anchor.kinematics.pos, anchor.kinematics.vel);

        /// <summary>The AIM-referent swap's entry: any resolved referent's plane kinematics (a rock aims exactly like a ship — same intercept policy, no new fire machinery).</summary>
        public void Aim(Vector2 referentPos, Vector2 referentVel)
        {
            if (pose == null) return;

            targetPos = referentPos;
            targetVel = referentVel;
            hasTarget = true;
            Target = AimPointFor(WeaponSlot.Primary);
        }

        /// <summary>Drops the aim without dropping the weapons: the slots stay the gunner's, it just has nothing to shoot at.</summary>
        public void HoldFire()
        {
            hasTarget = false;
            ClearTarget();
        }
    }
}

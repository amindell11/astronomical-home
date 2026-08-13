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

        private Vector2 enemyPos;
        private Vector2 enemyVel;
        private bool hasEnemy;

        /// <summary>The primary weapon's intercept point (world space), for diagnostics/gizmos.</summary>
        internal Vector3 Target { get; private set; }
        public bool HasTarget => hasEnemy;

        /// <summary>Muzzle speed of the primary weapon, used by the navigator for intercept lead.</summary>
        public float PrimaryProjectileSpeed => weapons?.ProjectileSpeed(WeaponSlot.Primary) ?? 0f;
        private void ClearTarget() => Target = Vector3.zero;

        public void Initialize(IWeaponContext weapons, IWeapons actuator, Func<Kinematics> poseFunc)
        {
            this.weapons = weapons;
            this.actuator = actuator;
            pose = poseFunc;
        }

        /// <summary>Drops the tracked enemy and aim point, restoring the freshly-initialized gunner.</summary>
        public void ResetState()
        {
            hasEnemy = false;
            enemyPos = default;
            enemyVel = default;
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
                var fire = engage && hasEnemy && (weapons.Sight(slot)?.Evaluate(AimPointFor(slot)) ?? false);
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
                return GamePlane.PlanePointToWorld(enemyPos);

            return GamePlane.PlanePointToWorld(
                AimPoint(pose(), enemyPos, enemyVel, weapons.ProjectileSpeed(slot)));
        }

        /// <summary>Consumes the fire lane's anchor (mirrors <c>Navigator.ApplyObjective</c>): stores enemy kinematics for per-slot firing solutions.</summary>
        public void Aim(in EnemyTarget anchor)
        {
            if (pose == null) return;

            enemyPos = anchor.kinematics.pos;
            enemyVel = anchor.kinematics.vel;
            hasEnemy = true;
            Target = AimPointFor(WeaponSlot.Primary);
        }

        /// <summary>Drops the aim without dropping the weapons: the slots stay the gunner's, it just has nothing to shoot at.</summary>
        public void HoldFire()
        {
            hasEnemy = false;
            ClearTarget();
        }
    }
}

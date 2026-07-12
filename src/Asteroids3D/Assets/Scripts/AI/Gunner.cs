using System;
using AI.States;
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
        public bool HasTarget => Target != Vector3.zero;

        /// <summary>Muzzle speed of the primary weapon, used by the navigator for intercept lead.</summary>
        public float PrimaryProjectileSpeed => weapons?.ProjectileSpeed(WeaponSlot.Primary) ?? 0f;
        private void ClearTarget() => Target = Vector3.zero;

        public void Initialize(IWeaponContext weapons, IWeapons actuator, Func<Kinematics> poseFunc)
        {
            this.weapons = weapons;
            this.actuator = actuator;
            pose = poseFunc;
        }

        /// <summary>
        /// Evaluates each equipped weapon independently — aiming with <em>that slot's</em>
        /// ballistics — and pushes a per-slot <see cref="WeaponCommand"/> to the actuator. The AI
        /// re-decides every step, so it reports both a press and a hold each step it wants fire
        /// ("mashing"); each weapon's own trigger semantics and conditions pace the actual shots.
        /// </summary>
        public void Fire()
        {
            if (weapons == null || actuator == null) return;

            var slots = weapons.Slots;
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var fire = hasEnemy && (weapons.Sight(slot)?.Evaluate(AimPointFor(slot)) ?? false);
                actuator.Fire(slot, new WeaponCommand { held = fire, pressed = fire });
            }
        }

        /// <summary>
        /// The world-space aim point for a slot: an intercept lead computed from the slot's own
        /// muzzle speed; a non-positive speed means hitscan — aim at the target's present position.
        /// </summary>
        internal Vector3 AimPointFor(WeaponSlot slot)
        {
            var speed = weapons.ProjectileSpeed(slot);
            if (speed <= 0f || pose == null)
                return GamePlane.PlanePointToWorld(enemyPos);

            return GamePlane.PlanePointToWorld(
                TargetingMath.PredictIntercept(pose(), enemyPos, enemyVel, speed));
        }

        /// <summary>
        /// Consumes the gunner slice of a <see cref="NavigationIntent"/>, mirroring
        /// <c>Navigator.ApplyIntent</c>. Stores the enemy kinematics for per-slot firing
        /// solutions; the cached <see cref="Target"/> is the primary weapon's lead (diagnostics
        /// and navigator consumers).
        /// </summary>
        public void ApplyIntent(in NavigationIntent intent)
        {
            if (!intent.isValid) return;

            if (!intent.enableFiring)
            {
                hasEnemy = false;
                ClearTarget();
                return;
            }

            if (intent.hasTarget && pose != null)
            {
                enemyPos = intent.target.kinematics.pos;
                enemyVel = intent.target.kinematics.vel;
                hasEnemy = true;
                Target = AimPointFor(WeaponSlot.Primary);
            }
        }
    }
}

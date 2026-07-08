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
    public partial class Gunner : MonoBehaviour
    {
        private IWeaponContext weapons;
        private IWeapons actuator;
        private Func<Kinematics> pose;
        private Vector3 Target { get; set; }
        public bool HasTarget => Target != Vector3.zero;

        /// <summary>Muzzle speed of the primary weapon, used by the navigator for intercept lead.</summary>
        public float PrimaryProjectileSpeed => weapons?.ProjectileSpeed(WeaponSlot.Primary) ?? 0f;
        private void SetTarget(Vector2 planePos) => Target = GamePlane.PlanePointToWorld(planePos);
        private void ClearTarget() => Target = Vector3.zero;

        public void Initialize(IWeaponContext weapons, IWeapons actuator, Func<Kinematics> poseFunc)
        {
            this.weapons = weapons;
            this.actuator = actuator;
            pose = poseFunc;
        }

        /// <summary>
        /// Evaluates each equipped weapon independently against the current target and pushes a
        /// per-slot <see cref="WeaponCommand"/> to the actuator. Each weapon owns its own fire
        /// decision via its <see cref="Gunsight"/>.
        /// </summary>
        public void Fire()
        {
            if (weapons == null || actuator == null) return;

            var slots = weapons.Slots;
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var fire = HasTarget && (weapons.Sight(slot)?.Evaluate(Target) ?? false);
                actuator.Fire(slot, new WeaponCommand { fire = fire });
            }
        }

        /// <summary>
        /// Consumes the gunner slice of a <see cref="NavigationIntent"/>, mirroring
        /// <c>Navigator.ApplyIntent</c>. The gunner owns its own firing solution: given the
        /// enemy kinematics it computes the intercept lead from its weapon's projectile speed.
        /// </summary>
        public void ApplyIntent(in NavigationIntent intent)
        {
            if (!intent.isValid) return;

            if (!intent.enableFiring)
            {
                ClearTarget();
                return;
            }

            if (intent.hasTarget && pose != null)
                SetTarget(TargetingMath.PredictIntercept(pose(), intent.target.kinematics.pos, intent.target.kinematics.vel, PrimaryProjectileSpeed));
        }
    }
}

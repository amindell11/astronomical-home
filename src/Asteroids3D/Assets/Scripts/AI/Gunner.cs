using System;
using AI.Context;
using AI.States;
using Combat;
using Combat.Weapons;
using Game;
using Movement;
using Ships;
using Ships.Command;
using UnityEngine;

namespace AI
{
    [DefaultExecutionOrder(-50)]
    public partial class Gunner : MonoBehaviour
    {
        private System.Func<State> getState;
        private Func<Kinematics> pose;
        private WeaponComponent primaryWeapon;
        private Gunsight primaryTrigger;
        private Gunsight secondaryTrigger;
        private Command currentCommand;
        public Command CurrentCommand => currentCommand;

        public Vector3 Target { get; private set; }
        public bool HasTarget => Target != Vector3.zero;

        public Vector3 FirePoint => (primaryWeapon != null && primaryWeapon.firePoint)
            ? primaryWeapon.firePoint.position
            : transform.position;

        public Vector2 TargetPlane => GamePlane.WorldPointToPlane(Target);
        public float AngleToTarget => (HasTarget && pose != null) ? TargetingMath.AngleTo(pose(), TargetPlane) : 0f;

        public float PrimaryProjectileSpeed =>
            primaryWeapon is Lasers laser ? laser.ProjectileSpeed : 0f;

        public void SetTarget(Vector3 worldPos) => Target = worldPos;
        public void SetTarget(Vector2 planePos) => Target = GamePlane.PlanePointToWorld(planePos);
        public void SetTarget(Transform target) => Target = target ? target.position : Vector3.zero;
        public void SetTarget(Ship enemy) => Target = enemy ? enemy.transform.position : Vector3.zero;
        public void ClearTarget() => Target = Vector3.zero;

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

            if (intent.hasGunnerEnemy && pose != null)
                SetTarget(TargetingMath.PredictIntercept(
                    pose(), intent.gunnerEnemyPos, intent.gunnerEnemyVel, PrimaryProjectileSpeed));
        }

        public void Initialize(WeaponComponent primary, WeaponComponent secondary, Func<Kinematics> pose, System.Func<State> stateProvider)
        {
            this.pose = pose;
            this.getState = stateProvider;
            primaryWeapon = primary;
            primaryTrigger = primary ? new Gunsight(primary, pose) : null;
            secondaryTrigger = secondary ? new Gunsight(secondary, pose) : null;
        }
        private void FixedUpdate()
        {
            if (getState == null) return;
            currentCommand = default;
            GenerateGunnerCommands(getState(), ref currentCommand);
        }

        private void GenerateGunnerCommands(State state, ref Command cmd)
        {
            if (!HasTarget) return;

            cmd.primaryFire = primaryTrigger?.Evaluate(Target) ?? false;
            cmd.secondaryFire = secondaryTrigger?.Evaluate(Target) ?? false;
        }
    }
}

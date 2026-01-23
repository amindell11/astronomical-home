using AI.Computers;
using AI.Context;
using Combat;
using Combat.Weapons;
using Game;
using Ships;
using UnityEngine;

namespace AI
{
    public partial class Gunner : MonoBehaviour
    {
        private System.Func<State> getState;
        private Targeting targeting;
        private WeaponComponent primaryWeapon;
        private WeaponComponent secondaryWeapon;
        protected Command currentCommand;
        public Command CurrentCommand => currentCommand;

        public Vector3 Target { get; private set; }
        public bool HasTarget => Target != Vector3.zero;

        public Vector3 FirePoint => (primaryWeapon != null && primaryWeapon.firePoint)
            ? primaryWeapon.firePoint.position
            : transform.position;

        public Vector2 TargetPlane => GamePlane.WorldPointToPlane(Target);
        public Vector2 VectorToTarget => (HasTarget && targeting != null) ? targeting.VectorTo(TargetPlane) : Vector2.zero;
        public float AngleToTarget => (HasTarget && targeting != null) ? targeting.AngleTo(TargetPlane) : 0f;

        public bool HasTargetLos => (HasTarget && targeting != null) && targeting.HasLineOfSight(FirePoint, Target, AngleToTarget);

        public void SetTarget(Vector3 worldPos) => Target = worldPos;
        public void SetTarget(Vector2 planePos) => Target = GamePlane.PlanePointToWorld(planePos);
        public void SetTarget(Transform target) => Target = target ? target.position : Vector3.zero;
        public void SetTarget(Ships.Ship enemy) => Target = enemy ? enemy.transform.position : Vector3.zero;
        public void ClearTarget() => Target = Vector3.zero;

        public void Initialize(WeaponComponent primary, WeaponComponent secondary, Targeting targeting, System.Func<State> stateProvider)
        {
            this.targeting = targeting;
            this.getState = stateProvider;
            primaryWeapon = primary;
            secondaryWeapon = secondary;
        }
        private void FixedUpdate(){
            if(getState !=null) {
                currentCommand = default;
                GenerateGunnerCommands(getState(), ref currentCommand);
            }
        }

        protected void GenerateGunnerCommands(State state, ref Command cmd)
        {
            if (!HasTarget) return;

            var context = new TargetingContext
            {
                TargetPosition = TargetPlane,
                DistanceToTarget = targeting.DistanceTo(TargetPlane),
                AngleToTarget = AngleToTarget,
                HasLineOfSight = HasTargetLos
            };

            cmd.PrimaryFire = primaryWeapon?.ShouldFire(context) ?? false;
            cmd.SecondaryFire = secondaryWeapon?.ShouldFire(context) ?? false;
        }
    }
}

using AI.Combat;
using AI.Computers;
using AI.Context;
using Game;
using Ships;
using UnityEngine;

namespace AI
{
    public partial class Gunner : MonoBehaviour
    {
        private Ship ship;
        private Targeting targeting;
        private IWeaponStrategy primary;
        private IWeaponStrategy secondary;

        public Vector3 Target { get; private set; }
        public bool HasTarget => Target != Vector3.zero;

        public Vector3 FirePoint => ship.Weapons.Primary?.firePoint
            ? ship.Weapons.Primary.firePoint.position
            : transform.position;

        public Vector2 TargetPlane => GamePlane.WorldPointToPlane(Target);
        public Vector2 VectorToTarget => HasTarget ? targeting.VectorTo(TargetPlane) : Vector2.zero;
        public float AngleToTarget => HasTarget ? targeting.AngleTo(TargetPlane) : 0f;

        public bool HasTargetLOS => HasTarget && targeting.HasLineOfSight(FirePoint, Target, AngleToTarget);

        public void SetTarget(Vector3 worldPos) => Target = worldPos;
        public void SetTarget(Vector2 planePos) => Target = GamePlane.PlanePointToWorld(planePos);
        public void SetTarget(Transform target) => Target = target ? target.position : Vector3.zero;
        public void SetTarget(Ships.Ship enemy) => Target = enemy ? enemy.transform.position : Vector3.zero;
        public void ClearTarget() => Target = Vector3.zero;

        public void Initialize(Ships.Ship ship, Targeting targeting)
        {
            this.ship = ship;
            this.targeting = targeting;

            if (ship.Weapons.Primary)
                primary = ship.Weapons.Primary.GetComponent<IWeaponStrategy>();
            if (ship.Weapons.Secondary)
                secondary = ship.Weapons.Secondary.GetComponent<IWeaponStrategy>();
        }

        public void GenerateGunnerCommands(State state, ref Command cmd)
        {
            if (!HasTarget) return;

            var context = new IWeaponStrategy.TargetingContext
            {
                TargetPosition = TargetPlane,
                DistanceToTarget = targeting.DistanceTo(TargetPlane),
                AngleToTarget = AngleToTarget,
                HasLineOfSight = HasTargetLOS
            };

            cmd.PrimaryFire = primary?.ShouldFire(context) ?? false;
            cmd.SecondaryFire = secondary?.ShouldFire(context) ?? false;
        }
    }
}

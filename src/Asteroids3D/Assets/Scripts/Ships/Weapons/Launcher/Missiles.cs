using System;
using Editor;
using Ships;
using Ships.Weapons.Conditions;
using UnityEngine;
using Utils;

namespace Weapons
{
    public class Missiles : LauncherBase<Missile>
    {
        [Header("Targeting")]
        [SerializeField] private TargetingComputer targetingComputer;

        public TargetingComputer Targeting => targetingComputer;

        private Rounds _rounds;

        protected override void Awake()
        {
            base.Awake();
            _rounds = GetComponent<Rounds>();
        }

        public override ProjectileBase Fire()
        {
            var proj = base.Fire() as Missile;

            if (!proj) return null;

            var lockedTarget = targetingComputer.ConsumeLock();
            if (lockedTarget != null)
                proj.SetTarget(lockedTarget.TargetPoint);
        
            return proj;
        }
        
#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!firePoint || _rounds == null) return;
            string ammoText = $"Ammo: {_rounds.AmmoCount}/{_rounds.MaxAmmo}";
            UnityEditor.Handles.Label(firePoint.position + Vector3.up * 2f, $"Missiles\n{ammoText}");
        }
#endif
    }
} 

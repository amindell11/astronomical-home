using System;
using Balance;
using Combat.Projectiles;
using UnityEngine;
using Combat.Weapons.Conditions;
using Substrate.Services.Projectiles;

namespace Combat.Weapons.Arsenal
{
    /// <summary>Announces each charge the weapon launches, so the owner's HUD can mark where it will stop.</summary>
    public interface IChargeLaunchReadout : IWeaponReadout
    {
        event Action<IChargeFlight> Launched;
    }

    /// <summary>Concussion charge launcher: semi-auto, each charge flies to the trigger's target point (see <see cref="Grenade.Launch"/>).</summary>
    public class Grenades : WeaponBase<Grenade>, IChargeLaunchReadout
    {
        [Header("Conditions")]
        [Stat, SerializeField] private Rounds rounds;
        [Stat, SerializeField] private Cooldown cooldown;

        public override bool AutoFire => false;
        public Rounds Rounds => rounds;
        public float BlastDamage => projectilePrefab.WavePrefab.MaxDamage;
        public float BlastRadius => projectilePrefab.WavePrefab.MaxRadius;
        public float Range => projectilePrefab.MaxDistance;

        public event Action<IChargeFlight> Launched;

        protected override void Awake()
        {
            base.Awake();
            if (!rounds) rounds = GetComponent<Rounds>();
            if (!cooldown) cooldown = GetComponent<Cooldown>();
        }

        public override ProjectileBase Fire(Vector3 targetPoint, IProjectileService projectiles)
        {
            var charge = (Grenade)base.Fire(targetPoint, projectiles);
            if (charge) Launched?.Invoke(charge);
            return charge;
        }
    }
}

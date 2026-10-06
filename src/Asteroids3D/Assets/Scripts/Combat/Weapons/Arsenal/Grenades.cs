using Balance;
using Combat.Projectiles;
using UnityEngine;
using Combat.Weapons.Conditions;

namespace Combat.Weapons.Arsenal
{
    /// <summary>Concussion charge launcher: semi-auto, each charge flies to the trigger's target point (see <see cref="Grenade.Launch"/>).</summary>
    public class Grenades : WeaponBase<Grenade>
    {
        [Header("Conditions")]
        [Stat, SerializeField] private Rounds rounds;
        [Stat, SerializeField] private Cooldown cooldown;

        public override bool AutoFire => false;
        public Rounds Rounds => rounds;
        public float BlastDamage => projectilePrefab.WavePrefab.MaxDamage;
        public float BlastRadius => projectilePrefab.WavePrefab.MaxRadius;
        public float Range => projectilePrefab.MaxDistance;

        protected override void Awake()
        {
            base.Awake();
            if (!rounds) rounds = GetComponent<Rounds>();
            if (!cooldown) cooldown = GetComponent<Cooldown>();
        }
    }
}

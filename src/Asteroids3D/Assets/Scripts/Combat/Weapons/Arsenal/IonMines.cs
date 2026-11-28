using Balance;
using Combat.Projectiles;
using Combat.Weapons.Conditions;
using Combat.Weapons.Mods;
using Substrate.Services.Projectiles;
using UnityEngine;

namespace Combat.Weapons.Arsenal
{
    /// <summary>
    /// Drops one ion mine at a time. An AI fires it only with a target inside the blast.
    /// </summary>
    public class IonMines : WeaponBase<IonMine>
    {
        [Header("Conditions")]
        [Stat, SerializeField] private Rounds rounds;
        [Stat, SerializeField] private Cooldown cooldown;

        private IonMine live;

        public override bool AutoFire => false;
        public override bool IsOrdnance => true;
        public Rounds Rounds => rounds;
        protected override WeaponCondition[] AuthoredConditions => new WeaponCondition[] { rounds, cooldown };
        public float BlastRadius => projectilePrefab.BlastPrefab.MaxRadius;
        public float StallSeconds => projectilePrefab.BlastPrefab.StallSeconds;

        protected override void Awake()
        {
            base.Awake();
            if (!rounds) rounds = GetComponent<Rounds>();
            if (!cooldown) cooldown = GetComponent<Cooldown>();
        }

        // The mine deals no damage, so damage mods never land on it.
        public override bool Supports(WeaponModField field) =>
            field != WeaponModField.Damage && base.Supports(field);

        public override ProjectileBase Fire(Vector3 targetPoint, IProjectileService projectiles)
        {
            var mine = (IonMine)base.Fire(targetPoint, projectiles);
            if (!mine) return null;
            live = mine;
            mine.ReturnedToPool += OnMineEnded;
            return mine;
        }

        public override bool CanFire() => !live && base.CanFire();

        public override bool InEnvelope(in TargetingContext context) =>
            context.distanceToTarget <= BlastRadius;

        public override bool HoldTrigger(TargetingContext context) =>
            CanFire() && InEnvelope(in context);

        public override void Reset()
        {
            base.Reset();
            OnMineEnded();
        }

        private void OnMineEnded()
        {
            if (live)
                live.ReturnedToPool -= OnMineEnded;
            live = null;
        }
    }
}

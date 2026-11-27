using Balance;
using Combat.Projectiles;
using UnityEngine;
using Combat.Weapons.Conditions;
using Combat.Weapons.Mods;

namespace Combat.Weapons.Arsenal
{
    /// <summary>Ballistic autocannon: magazine-fed (<see cref="Rounds"/>), no heat, laser-class straight-line slugs.</summary>
    public class Rippers : WeaponBase<Laser>
    {
        [Header("AI Firing")]
        [Tooltip("Max distance at which an AI gunner will open fire.")]
        [Stat, SerializeField, Min(0f)] private float fireDistance = 18f;
        [Tooltip("Max aim error (degrees) at which an AI gunner will open fire.")]
        [Stat, SerializeField, Range(0f, 180f)] private float fireAngleTolerance = 6f;

        [Header("Suppression")]
        [SerializeField] private WeaponBehaviourMod suppression;
        [Tooltip("With Suppression held: this many hits on one ship inside the window suppress it.")]
        [Stat, SerializeField, Min(1)] private int suppressionHits = 5;
        [Tooltip("With Suppression held: seconds the last hits must all land within.")]
        [Stat, SerializeField, Min(0f)] private float suppressionWindow = 0.5f;
        [Tooltip("With Suppression held: seconds each steady hit keeps the target slowed and off its keystone.")]
        [Stat, SerializeField, Min(0.01f)] private float suppressionHold = 0.4f;

        [Header("Conditions")]
        [Stat, SerializeField] private Rounds rounds;
        [Stat, SerializeField] private Cooldown cooldown;

        private SuppressingFire suppressing;

        public override FirePattern FirePattern => FirePattern.Alternating;
        public override float ProjectileSpeed => projectilePrefab.LaserSpeed;
        public override float FireRange => fireDistance;
        public Rounds Rounds => rounds;
        protected override WeaponCondition[] AuthoredConditions => new WeaponCondition[] { rounds, cooldown };
        public float Damage => projectilePrefab.Damage;
        public float? ShotsPerSecond =>
            cooldown && cooldown.SecondsBetweenShots > 0f ? 1f / cooldown.SecondsBetweenShots : (float?)null;

        protected override void Awake()
        {
            base.Awake();
            if (!rounds) rounds = GetComponent<Rounds>();
            if (!cooldown) cooldown = GetComponent<Cooldown>();
            suppressing = new SuppressingFire(suppressionHits, suppressionWindow, suppressionHold);
        }

        protected override void Prime(Laser slug)
        {
            if (Behaviours.Holds(suppression)) slug.Suppress(suppressing);
        }

        public override bool InEnvelope(in TargetingContext context) =>
            context.Within(fireDistance, fireAngleTolerance);

        public override bool HoldTrigger(TargetingContext context) =>
            Rounds && Rounds.CanFire() && InEnvelope(in context);
    }
}

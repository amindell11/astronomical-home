using Balance;
using Combat.Projectiles;
using UnityEngine;
using Combat.Weapons.Conditions;

namespace Combat.Weapons
{
    /// <summary>Concussion charge dropper: semi-auto, releases backward (see <see cref="Grenade.Launch"/>) at a close pursuer.</summary>
    public class Grenades : WeaponBase<Grenade>
    {
        [Header("AI Firing")]
        [Tooltip("Max distance at which an AI gunner drops a charge on a pursuer.")]
        [Stat, SerializeField, Min(0f)] private float dropRange = 12f;
        [Tooltip("Min angle off the nose (degrees) before the AI drops — the target must be behind.")]
        [Stat, SerializeField, Range(0f, 180f)] private float minDropAngle = 120f;

        [Header("Conditions")]
        [Stat, SerializeField] private Rounds rounds;
        [Stat, SerializeField] private Cooldown cooldown;

        public override bool AutoFire => false;
        public Rounds Rounds => rounds;
        public float BlastDamage => projectilePrefab.WavePrefab.MaxDamage;
        public float BlastRadius => projectilePrefab.WavePrefab.MaxRadius;
        public float FuseSeconds => projectilePrefab.FuseSeconds;

        protected override void Awake()
        {
            base.Awake();
            if (!rounds) rounds = GetComponent<Rounds>();
            if (!cooldown) cooldown = GetComponent<Cooldown>();
        }

        // Behind-arc drop: no LOS term by design — the charge releases backward at a pursuer.
        public override bool InEnvelope(in TargetingContext context) =>
            context.distanceToTarget <= dropRange && context.angleToTarget >= minDropAngle;

        public override bool ShouldFire(TargetingContext context) => InEnvelope(in context);
    }
}

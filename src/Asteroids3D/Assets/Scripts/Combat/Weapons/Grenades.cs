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
        [SerializeField, Min(0f)] private float dropRange = 12f;
        [Tooltip("Min angle off the nose (degrees) before the AI drops — the target must be behind.")]
        [SerializeField, Range(0f, 180f)] private float minDropAngle = 120f;

        [Header("Conditions")]
        [SerializeField] private Rounds rounds;

        public override bool AutoFire => false;

        public override string HangarStats
        {
            get
            {
                var wave = projectilePrefab ? projectilePrefab.WavePrefab : null;
                if (!wave) return DisplayName;
                var mag = rounds
                    ? $"   |   {rounds.MaxAmmo} charges" + (rounds.ReloadTime > 0f ? $" (reload {rounds.ReloadTime:0.#}s)" : "")
                    : "";
                return $"Blast {wave.MaxDamage:0} to {wave.MaxRadius:0}u, hits friend and foe{mag}   |   Fuse {projectilePrefab.FuseSeconds:0.#}s";
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (!rounds) rounds = GetComponent<Rounds>();
        }

        // Behind-arc drop: no LOS term by design — the charge releases backward at a pursuer.
        public override bool InEnvelope(in TargetingContext context) =>
            context.distanceToTarget <= dropRange && context.angleToTarget >= minDropAngle;

        public override bool ShouldFire(TargetingContext context) => InEnvelope(in context);
    }
}

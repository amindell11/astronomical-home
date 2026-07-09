using Combat.Conditions;
using Combat.Projectile;
using UnityEngine;

namespace Combat.Weapons
{
    /// <summary>
    /// Concussion charge dropper. Semi-auto; the charge releases backward from the fire
    /// direction (see <see cref="Grenade.Launch"/>), so the AI drops one when its target is
    /// chasing close behind. Line of sight is ignored: <see cref="Combat.LosCache"/>
    /// short-circuits to false beyond a small aim cone, so a behind-target never reports LOS.
    /// </summary>
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

        public override bool ShouldFire(TargetingContext context)
        {
            return context.distanceToTarget <= dropRange && context.angleToTarget >= minDropAngle;
        }
    }
}

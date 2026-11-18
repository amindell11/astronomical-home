using UnityEngine;

namespace Damage
{
    public readonly struct AppliedDamage
    {
        public readonly DamageInfo Info;
        public readonly float Shield;
        public readonly float Hull;

        public AppliedDamage(in DamageInfo hit, float shield, float hull)
        {
            Info = hit.WithAmount(shield + hull);
            Shield = shield;
            Hull = hull;
        }

        // Shields soften a hit; a hit taking this share of max hull is full size.
        private const float ShieldShare = .5f;
        private const float FullHitShare = .25f;

        public float HitSize(float maxHull) => Mathf.Clamp01((Hull + Shield * ShieldShare) / (maxHull * FullHitShare));
    }
}

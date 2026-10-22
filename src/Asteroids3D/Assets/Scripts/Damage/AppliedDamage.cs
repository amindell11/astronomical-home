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
    }
}

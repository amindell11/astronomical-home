using Ships.Damage;

namespace UI.PlayerState.Alerts
{
    public static class HullCritical
    {
        public const float HullThreshold = .25f;
        public const float ShieldThreshold = .1f;

        public static bool Holds(float hullFraction, float shieldFraction) =>
            hullFraction > 0f && hullFraction <= HullThreshold && shieldFraction <= ShieldThreshold;

        public static bool Holds(IDamageEvents damage) => Holds(damage.Health.Pct, damage.Shield.Pct);
    }
}

using UnityEngine;

namespace Asteroids
{
    public readonly struct CollisionDamageLaw
    {
        public readonly float GraceDeltaV;
        public readonly float DamagePerDeltaV;
        public readonly float Lethality;

        public CollisionDamageLaw(float graceDeltaV, float damagePerDeltaV, float lethality)
        {
            GraceDeltaV = graceDeltaV;
            DamagePerDeltaV = damagePerDeltaV;
            Lethality = lethality;
        }

        public float Damage(float deltaV) =>
            Mathf.Max(0f, deltaV - GraceDeltaV) * DamagePerDeltaV * Lethality;
    }
}

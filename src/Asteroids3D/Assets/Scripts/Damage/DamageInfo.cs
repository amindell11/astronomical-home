using Ships;
using UnityEngine;
using Ships.Registry;

namespace Damage
{
    /// <summary>What produced a hit — weapon class or collision.</summary>
    public enum DamageKind
    {
        Laser,
        Railgun,
        Missile,
        ConcussionWave,
        Collision,
    }

    /// <summary>
    /// Per-hit context, built at the producer's call site and carried through
    /// <see cref="IDamageable.TakeDamage"/> onto the damage events.
    /// <see cref="AttackerId"/> is <see cref="ShipId.Invalid"/> when no ship caused the hit
    /// (asteroid collision).
    /// </summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly DamageKind Kind;
        public readonly ShipId AttackerId;
        public readonly float HitMass;
        public readonly Vector3 HitVelocity;
        public readonly Vector3 HitPoint;

        public DamageInfo(float amount, DamageKind kind, ShipId attackerId,
                          float hitMass, Vector3 hitVelocity, Vector3 hitPoint)
        {
            Amount = amount;
            Kind = kind;
            AttackerId = attackerId;
            HitMass = hitMass;
            HitVelocity = hitVelocity;
            HitPoint = hitPoint;
        }

        /// <summary>Same hit with a different amount — events report applied damage, not incoming.</summary>
        public DamageInfo WithAmount(float amount) =>
            new(amount, Kind, AttackerId, HitMass, HitVelocity, HitPoint);
    }
}

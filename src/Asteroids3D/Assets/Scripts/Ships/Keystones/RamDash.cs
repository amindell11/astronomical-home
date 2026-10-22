using Asteroids;
using Balance;
using Damage;
using Movement;
using UnityEngine;

namespace Ships.Keystones
{
    /// <summary>
    /// Bulwark's kit: a dash that is a live ram for a short window or until its first contact,
    /// immune to collision damage meanwhile. The first contact resolves it: a ship takes ram damage
    /// scaled by how square the hit is, a lighter rock shatters, a heavier one just stops the ship.
    /// That contact's own step stays immune, so the rock's collision damage on it is dropped too.
    /// </summary>
    public sealed class RamDash : Dash
    {
        [Tooltip("Seconds after activation during which the dash is a live ram, until its first contact.")]
        [Stat, SerializeField] private float window = 0.6f;

        [Tooltip("Damage a head-on ram deals a ship; a glancing hit deals less.")]
        [Stat, SerializeField] private float shipDamage = 60f;

        [Tooltip("How far off the nose, in degrees, an enemy may sit for an AI to fire the ram at it.")]
        [Stat, SerializeField] private float sightAngle = 15f;

        private bool resolved;
        private float resolvedAt;

        public override string DisplayName => "Ram";
        public float Window => window;
        public float ShipDamage => shipDamage;
        public bool RamLive => SinceActivation < window && (!resolved || SinceActivation == resolvedAt);
        public override bool Active => RamLive;
        public override bool SeeksContact => RamLive;

        public override bool Blocks(in DamageInfo hit) => hit.Kind == DamageKind.Collision && RamLive;

        public override bool ShouldActivate(in KeystoneContext context)
        {
            var offset = context.Target.pos - context.Self.pos;
            var range = offset.magnitude;
            if (range < 1e-3f) return false;
            var toward = offset / range;
            if (Vector2.Dot(context.Self.Forward, toward) < Mathf.Cos(sightAngle * Mathf.Deg2Rad)) return false;
            if (Vector2.Dot(context.Self.vel - context.Target.vel, toward) < 0f) return false;
            // The rigidbody clamps to the speed cap, so a dash at the cap adds no closing speed.
            var dashed = Vector2.ClampMagnitude(context.Self.vel + context.Self.Forward * (Impulse / context.SelfMass), context.SelfMaxSpeed);
            return range <= Vector2.Dot(dashed - context.Target.vel, toward) * window;
        }

        public override void OnContact(in KeystoneContact contact)
        {
            if (!RamLive || resolved) return;
            resolved = true;
            resolvedAt = SinceActivation;

            var target = contact.Other.GetComponentInParent<IDamageable>();
            if (target == null) return;
            if (target is AsteroidDamage)
            {
                if (!contact.OtherBody || contact.OtherBody.mass >= contact.SelfMass) return;
                target.TakeDamage(Hit(float.MaxValue, in contact));
            }
            else
                target.TakeDamage(Hit(shipDamage * contact.ClosingFraction, in contact));
            CreditActivation();
        }

        protected override KeystoneEffect Activate(in Kinematics kinematics, Vector2? target)
        {
            resolved = false;
            return base.Activate(in kinematics, target);
        }

        private static DamageInfo Hit(float amount, in KeystoneContact contact) =>
            new(amount, DamageKind.Collision, contact.Self, contact.SelfMass, contact.SelfVelocity, contact.Point);
    }
}

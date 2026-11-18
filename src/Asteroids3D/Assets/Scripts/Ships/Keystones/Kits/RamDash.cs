using Asteroids;
using Balance;
using Damage;
using Movement;
using UnityEngine;

namespace Ships.Keystones.Kits
{
    /// <summary>
    /// Bulwark's kit brakes to a full stop on positioning thrusters, unprotected, while the pilot
    /// keeps yaw, then dashes along the nose. The live ram counts from that launch until the window
    /// ends or the first contact: a ship takes damage scaled by how square the hit is, a lighter rock
    /// shatters, a heavier one stops the ship. That contact's own step stays immune.
    /// </summary>
    public sealed class RamDash : Dash
    {
        [Tooltip("Seconds the positioning thrusters brake the ship to a full stop before the ram launches.")]
        [Stat, SerializeField] private float brake = 0.3f;

        [Tooltip("Seconds after the launch during which the dash is a live ram, until its first contact.")]
        [Stat, SerializeField] private float window = 0.6f;

        [Tooltip("Damage a head-on ram deals a ship; a glancing hit deals less.")]
        [Stat, SerializeField] private float shipDamage = 60f;

        [Tooltip("How far off the nose, in degrees, an enemy may sit for an AI to fire the ram at it.")]
        [Stat, SerializeField] private float sightAngle = 15f;

        // -1 idle, -2 not yet counted, 0 launches this step.
        private int brakeStepsLeft = -1;
        private bool launched;
        private float launchedAt;
        private bool resolved;
        private float resolvedAt;

        public override string DisplayName => "Ram";
        public float Window => window;
        public float ShipDamage => shipDamage;
        public bool Braking => brakeStepsLeft != -1;
        private float SinceLaunch => SinceActivation - launchedAt;
        public bool RamLive => launched && SinceLaunch < window && (!resolved || SinceLaunch == resolvedAt);
        public override bool Active => RamLive;
        public override bool SeeksContact => Braking || RamLive;
        public override bool FliesNavigatorHeading => true;
        public override bool HoldsThrottle => Braking;

        public override bool Blocks(in DamageInfo hit) => hit.Kind == DamageKind.Collision && RamLive;

        public override bool ShouldActivate(in KeystoneContext context)
        {
            var offset = context.Target.pos - context.Self.pos;
            var range = offset.magnitude;
            if (range < 1e-3f) return false;
            var toward = offset / range;
            if (Vector2.Dot(context.Self.Forward, toward) < Mathf.Cos(sightAngle * Mathf.Deg2Rad)) return false;
            // At launch the target has flown on; Bulwark, decelerating linearly, covered half its speed.
            var atLaunch = offset + (context.Target.vel - context.Self.vel * 0.5f) * brake;
            var reach = atLaunch.magnitude;
            if (reach < 1e-3f) return true;
            var dashed = Vector2.ClampMagnitude(context.Self.Forward * (Impulse / context.SelfMass), context.SelfMaxSpeed);
            return reach <= Vector2.Dot(dashed - context.Target.vel, atLaunch / reach) * window;
        }

        public override void OnContact(in KeystoneContact contact)
        {
            if (!RamLive || resolved) return;
            resolved = true;
            resolvedAt = SinceLaunch;

            if (!DamageableColliders.TryGet(contact.Other, out var target)) return;
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
            launched = false;
            brakeStepsLeft = -2;
            return default;
        }

        protected override KeystoneEffect Sustain(in Kinematics kinematics, float dt, float baseMaxSpeed)
        {
            if (brakeStepsLeft == -1) return default;
            if (brakeStepsLeft < 0)
                brakeStepsLeft = Mathf.Max(1, Mathf.RoundToInt(brake / dt));
            if (brakeStepsLeft > 0)
            {
                var accel = -kinematics.vel / (brakeStepsLeft * dt);
                brakeStepsLeft--;
                return new KeystoneEffect(Vector2.zero, Vector2.zero, accel);
            }

            brakeStepsLeft = -1;
            launched = true;
            launchedAt = SinceActivation;
            return kinematics.Forward * Impulse;
        }

        protected override void OnReset()
        {
            brakeStepsLeft = -1;
            launched = false;
            resolved = false;
        }

        private static DamageInfo Hit(float amount, in KeystoneContact contact) =>
            new(amount, DamageKind.Collision, contact.Self, contact.SelfMass, contact.SelfVelocity, contact.Point);
    }
}

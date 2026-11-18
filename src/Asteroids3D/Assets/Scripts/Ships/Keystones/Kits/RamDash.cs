using Asteroids;
using Balance;
using Damage;
using Movement;
using Substrate;
using UnityEngine;

namespace Ships.Keystones.Kits
{
    /// <summary>
    /// Bulwark's kit brakes to a full stop on positioning thrusters, unprotected, while the pilot
    /// keeps yaw, then dashes along the nose. While the ram is live the speed cap lifts, a burn
    /// drives along the launch line, and the ship is heavier. The first contact knocks the target
    /// back; a ship takes damage that grows with impact speed and how square the hit is, a lighter
    /// rock shatters, a heavier one stops the ship. That contact's own step stays immune.
    /// </summary>
    public sealed class RamDash : Dash
    {
        [Tooltip("Seconds the positioning thrusters brake the ship to a full stop before the ram launches.")]
        [Stat, SerializeField] private float brake = 0.5f;

        [Tooltip("Seconds after the launch during which the dash is a live ram, until its first contact.")]
        [Stat, SerializeField] private float window = 0.6f;

        [Tooltip("Damage a head-on ram deals a ship at the reference impact speed; a faster hit deals more, a glancing hit less.")]
        [Stat, SerializeField] private float shipDamage = 60f;

        [Tooltip("How far off the nose, in degrees, an enemy may sit for an AI to fire the ram at it.")]
        [Stat, SerializeField] private float sightAngle = 15f;

        [Tooltip("Multiplier on the speed cap while the ram is live.")]
        [Stat, SerializeField] private float surgeSpeedScale = 1.5f;

        [Tooltip("Acceleration along the launch line while the ram is live, in units per second squared.")]
        [Stat, SerializeField] private float surgeAcceleration = 25f;

        [Tooltip("Multiplier on the ship's mass while the ram is live.")]
        [Stat, SerializeField] private float massScale = 2f;

        [Tooltip("Impulse (N·s) a head-on ram gives its target along the launch line; a glancing hit gives less.")]
        [Stat, SerializeField] private float knockback = 12000f;

        [Tooltip("Impact speed, in units per second, at which a head-on ram deals its ship damage.")]
        [Stat, SerializeField] private float damageReferenceSpeed = 16f;

        // -1 idle, -2 not yet counted, 0 launches this step.
        private int brakeStepsLeft = -1;
        private int brakeSteps;
        private bool launched;
        private float launchedAt;
        private bool resolved;
        private float resolvedAt;
        private Vector2 launchDirection;

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
        public override float HoldProgress => Braking && brakeSteps > 0 ? 1f - (float)brakeStepsLeft / brakeSteps : 0f;
        public override float SpeedScale => RamLive ? surgeSpeedScale : 1f;
        public override float PeakSpeedScale => surgeSpeedScale;
        public override float MassScale => RamLive ? massScale : 1f;

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
            var launchSpeed = Mathf.Min(Impulse / context.SelfMass, context.SelfMaxSpeed * surgeSpeedScale);
            var travel = Mathf.Min(launchSpeed * window + 0.5f * surgeAcceleration * window * window,
                context.SelfMaxSpeed * surgeSpeedScale * window);
            toward = atLaunch / reach;
            return reach <= Vector2.Dot(context.Self.Forward, toward) * travel - Vector2.Dot(context.Target.vel, toward) * window;
        }

        public override void OnContact(in KeystoneContact contact)
        {
            if (!RamLive || resolved) return;
            resolved = true;
            resolvedAt = SinceLaunch;

            if (!DamageableColliders.TryGet(contact.Other, out var target)) return;
            if (contact.OtherBody && !contact.OtherBody.isKinematic)
                contact.OtherBody.AddForceAtPosition(
                    GamePlane.PlaneDirToWorld(launchDirection) * (knockback * contact.ClosingFraction),
                    contact.Point, ForceMode.Impulse);
            if (target is AsteroidDamage)
            {
                if (!contact.OtherBody || contact.OtherBody.mass >= contact.SelfMass) return;
                target.TakeDamage(Hit(float.MaxValue, in contact));
            }
            else
                target.TakeDamage(Hit(
                    shipDamage * contact.ClosingFraction * (contact.RelativeSpeed / damageReferenceSpeed), in contact));
            CreditActivation();
        }

        protected override KeystoneEffect Activate(in Kinematics kinematics, Vector2? target)
        {
            resolved = false;
            launched = false;
            launchDirection = Vector2.zero;
            brakeStepsLeft = -2;
            brakeSteps = 0;
            return default;
        }

        protected override KeystoneEffect Sustain(in Kinematics kinematics, float dt, float baseMaxSpeed)
        {
            if (brakeStepsLeft == -1)
                return RamLive
                    ? new KeystoneEffect(Vector2.zero, Vector2.zero, launchDirection * surgeAcceleration)
                    : default;
            if (brakeStepsLeft < 0)
            {
                brakeStepsLeft = Mathf.Max(1, Mathf.RoundToInt(brake / dt));
                brakeSteps = brakeStepsLeft;
            }
            if (brakeStepsLeft > 0)
            {
                var accel = -kinematics.vel / (brakeStepsLeft * dt);
                brakeStepsLeft--;
                return new KeystoneEffect(Vector2.zero, Vector2.zero, accel);
            }

            brakeStepsLeft = -1;
            launched = true;
            launchedAt = SinceActivation;
            launchDirection = kinematics.Forward;
            // Impulse is momentum, so it scales with the heavier mass to keep launch speed.
            return kinematics.Forward * (Impulse * massScale);
        }

        protected override void OnReset()
        {
            brakeStepsLeft = -1;
            brakeSteps = 0;
            launched = false;
            resolved = false;
            launchDirection = Vector2.zero;
        }

        private static DamageInfo Hit(float amount, in KeystoneContact contact) =>
            new(amount, DamageKind.Collision, contact.Self, contact.SelfMass, contact.SelfVelocity, contact.Point);
    }
}

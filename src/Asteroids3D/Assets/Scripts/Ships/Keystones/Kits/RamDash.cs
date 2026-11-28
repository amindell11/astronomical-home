using Asteroids;
using Balance;
using Damage;
using Movement;
using Ships.Damage;
using Ships.Mods;
using Substrate;
using UnityEngine;
using Ships.Presentation;

namespace Ships.Keystones.Kits
{
    /// <summary>
    /// Bulwark's kit brakes to a full stop on positioning thrusters while the pilot keeps yaw, then
    /// dashes along the nose. Through the brake and the live ram the ship braces: every hit from ahead
    /// is absorbed, while the sides and rear stay open. While the ram is live the speed cap lifts, a
    /// burn drives along the launch line, and the ship is heavier. Damage and knockback follow how
    /// centred the ram's aim is, and speed only adds damage; a lighter rock shatters, a heavier one
    /// stops the ship, and Battering ram raises that limit. A ram that makes contact refunds part of
    /// its cooldown, and with Chain ram one that kills a ship refunds all of it; one that hits nothing
    /// keeps the full cooldown and sheds its surge speed.
    /// </summary>
    public sealed class RamDash : Dash
    {
        private static readonly Word RefundWord = new("REFUND", new Color(.4f, 1f, .6f));

        [Tooltip("Seconds the positioning thrusters brake the ship to a full stop before the ram launches.")]
        [Stat, SerializeField] private float brake = 0.4f;

        [Tooltip("Degrees off the nose, seen from the ship's centre, inside which the brace absorbs a hit.")]
        [Stat, SerializeField] private float braceAngle = 60f;

        [Tooltip("Seconds after the launch during which the dash is a live ram, until its first contact.")]
        [Stat, SerializeField] private float window = 0.45f;

        [Tooltip("Cooldown, in seconds from the press, once the ram makes contact; a ram that hits nothing keeps the full cooldown.")]
        [Stat, SerializeField] private float hitCooldown = 3.5f;

        [Tooltip("Seconds an unspent ram takes to shed its surge speed back to normal top speed.")]
        [Stat, SerializeField] private float missDump = 0.3f;

        [Tooltip("Damage a centred ram deals a ship. Impact speed can only raise it, and a less centred aim deals less.")]
        [Stat, SerializeField] private float shipDamage = 80f;

        [Tooltip("Degrees off the target's centre still dealing a full hit.")]
        [Stat, SerializeField] private float aimFullAngle = 30f;

        [Tooltip("Degrees off the target's centre at which a hit has fallen to the aim floor.")]
        [Stat, SerializeField] private float aimFloorAngle = 75f;

        [Tooltip("Share of a full hit the worst clip still deals.")]
        [Stat, SerializeField] private float aimFloor = 0.5f;

        [Tooltip("How far off the nose, in degrees, an enemy may sit for an AI to fire the ram at it.")]
        [Stat, SerializeField] private float sightAngle = 15f;

        [Tooltip("Multiplier on the speed cap while the ram is live.")]
        [Stat, SerializeField] private float surgeSpeedScale = 1.5f;

        [Tooltip("Acceleration along the launch line while the ram is live, in units per second squared.")]
        [Stat, SerializeField] private float surgeAcceleration = 25f;

        [Tooltip("Multiplier on the ship's mass while the ram is live.")]
        [Stat, SerializeField] private float massScale = 2f;

        [Tooltip("Impulse (N·s) a centred ram gives its target along the launch line; a less centred aim gives less.")]
        [Stat, SerializeField] private float knockback = 12000f;

        [Tooltip("Most speed, in units per second, the knockback adds to its target.")]
        [Stat, SerializeField] private float maxKnockbackSpeed = 15f;

        [Tooltip("Impact speed, in units per second, from which hits start dealing more than a full hit.")]
        [Stat, SerializeField] private float damageReferenceSpeed = 16f;

        [Tooltip("Largest damage multiplier impact speed gives; slower hits never deal less than a full hit.")]
        [Stat, SerializeField] private float maxSpeedBonus = 1.6f;

        [Tooltip("The Battering ram keystone mod, whose holding raises the heaviest rock the ram shatters.")]
        [SerializeField] private KeystoneMod batteringRam;

        [Tooltip("With Battering ram, the heaviest rock the live ram shatters, as a multiple of the ship's ramming mass.")]
        [Stat, SerializeField] private float batteringRamMass = 3f;

        [Tooltip("The Chain ram keystone mod, whose holding makes a ram that kills a ship refund its whole cooldown.")]
        [SerializeField] private KeystoneMod chainRam;

        // -1 idle, -2 not yet counted, 0 launches this step.
        private int brakeStepsLeft = -1;
        private int brakeSteps;
        private bool launched;
        private float launchedAt;
        private bool resolved;
        private float resolvedAt;
        private Vector2 launchDirection;
        private Vector2 nose;
        private int dumpStepsLeft;
        private bool missed;

        public override string DisplayName => "Ram";
        public float Window => window;
        public float ShipDamage => shipDamage;
        public float BraceAngle => braceAngle;
        public int BracedHits { get; private set; }
        public Vector3 LastBracedPoint { get; private set; }
        public bool Braking => brakeStepsLeft != -1;
        public bool Bracing => Braking || RamLive;
        private float SinceLaunch => SinceActivation - launchedAt;
        public bool RamLive => launched && SinceLaunch < window && (!resolved || SinceLaunch == resolvedAt);
        public override bool Active => RamLive;
        public override bool SeeksContact => Bracing;
        public override bool ContactsByHull => RamLive;
        // The servo aims the nose everywhere except the live ram, which holds its launch line.
        public override bool FliesNavigatorHeading => RamLive;
        public override bool HoldsThrottle => Braking;
        public override float HoldProgress => Braking && brakeSteps > 0 ? 1f - (float)brakeStepsLeft / brakeSteps : 0f;
        public override float SpeedScale => RamLive ? surgeSpeedScale : 1f;
        public override float PeakSpeedScale => surgeSpeedScale;
        public override float MassScale => RamLive ? massScale : 1f;

        public override bool Blocks(in DamageInfo hit)
        {
            if (!Bracing) return false;
            var toHit = GamePlane.WorldPointToPlane(hit.HitPoint) - GamePlane.WorldPointToPlane(Body.worldCenterOfMass);
            return Vector2.Angle(nose, toHit) <= braceAngle;
        }

        public override void NotifyBlocked(DamageInfo hit)
        {
            if (!Blocks(in hit)) return;
            BracedHits++;
            LastBracedPoint = hit.HitPoint;
        }

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
            // The ram lands when the hulls touch, short of the centres meeting.
            var gap = reach - Host.Radius - context.TargetRadius;
            var launchSpeed = Mathf.Min(Impulse / context.SelfMass, context.SelfMaxSpeed * surgeSpeedScale);
            var travel = Mathf.Min(launchSpeed * window + 0.5f * surgeAcceleration * window * window,
                context.SelfMaxSpeed * surgeSpeedScale * window);
            toward = atLaunch / reach;
            return gap <= Vector2.Dot(context.Self.Forward, toward) * travel - Vector2.Dot(context.Target.vel, toward) * window;
        }

        public override void OnContact(in KeystoneContact contact)
        {
            if (!RamLive || resolved) return;
            resolved = true;
            resolvedAt = SinceLaunch;
            ShortenCooldown(hitCooldown);
            RecordHit(contact.Point);

            if (!DamageableColliders.TryGet(contact.Other, out var target)) return;
            var aim = Aim(in contact);
            if (contact.OtherBody && !contact.OtherBody.isKinematic)
                contact.OtherBody.AddForceAtPosition(
                    GamePlane.PlaneDirToWorld(launchDirection) * Mathf.Min(
                        knockback * aim, contact.OtherBody.mass * maxKnockbackSpeed),
                    contact.Point, ForceMode.Impulse);
            if (target is AsteroidDamage)
            {
                var heaviest = contact.SelfMass * (Behaviours.Holds(batteringRam) ? batteringRamMass : 1f);
                if (!contact.OtherBody || contact.OtherBody.mass >= heaviest) return;
                target.TakeDamage(Hit(float.MaxValue, in contact));
            }
            else
            {
                target.TakeDamage(Hit(
                    shipDamage * aim * Mathf.Clamp(contact.RelativeSpeed / damageReferenceSpeed, 1f, maxSpeedBonus),
                    in contact));
                if (Behaviours.Holds(chainRam) && target is DamageController victim && victim.Health.CurrentValue <= 0f)
                {
                    ShortenCooldown(0f);
                    Pop(RefundWord, contact.Point);
                }
            }
            CreditActivation();
        }

        protected override KeystoneEffect Activate(in Kinematics kinematics, Vector2? target)
        {
            resolved = false;
            launched = false;
            launchDirection = Vector2.zero;
            brakeStepsLeft = -2;
            brakeSteps = 0;
            missed = false;
            dumpStepsLeft = 0;
            return default;
        }

        protected override KeystoneEffect Sustain(in Kinematics kinematics, float dt, float baseMaxSpeed)
        {
            nose = kinematics.Forward;
            if (brakeStepsLeft != -1)
                return BrakeOrLaunch(in kinematics, dt);
            if (RamLive)
                return new KeystoneEffect(Vector2.zero, Vector2.zero, launchDirection * surgeAcceleration);
            return ShedMiss(in kinematics, dt, baseMaxSpeed);
        }

        private KeystoneEffect BrakeOrLaunch(in Kinematics kinematics, float dt)
        {
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

        private KeystoneEffect ShedMiss(in Kinematics kinematics, float dt, float baseMaxSpeed)
        {
            if (!missed && launched && !resolved && SinceLaunch >= window)
            {
                missed = true;
                if (missDump > 0f)
                    dumpStepsLeft = Mathf.Max(1, Mathf.RoundToInt(missDump / dt));
            }
            if (dumpStepsLeft <= 0)
                return default;

            var speed = kinematics.vel.magnitude;
            var accel = speed > baseMaxSpeed
                ? -kinematics.vel / speed * ((speed - baseMaxSpeed) / (dumpStepsLeft * dt))
                : Vector2.zero;
            dumpStepsLeft--;
            return new KeystoneEffect(Vector2.zero, Vector2.zero, accel);
        }

        protected override void OnReset()
        {
            brakeStepsLeft = -1;
            brakeSteps = 0;
            launched = false;
            resolved = false;
            launchDirection = Vector2.zero;
            nose = Vector2.zero;
            missed = false;
            dumpStepsLeft = 0;
        }

        private float Aim(in KeystoneContact contact)
        {
            var centre = contact.OtherBody ? contact.OtherBody.worldCenterOfMass : contact.Other.bounds.center;
            var offset = GamePlane.WorldPointToPlane(centre) - GamePlane.WorldPointToPlane(Body.worldCenterOfMass);
            var angle = offset.sqrMagnitude < 1e-6f ? 0f : Vector2.Angle(launchDirection, offset);
            if (angle <= aimFullAngle) return 1f;
            return Mathf.Lerp(1f, aimFloor, Mathf.InverseLerp(aimFullAngle, aimFloorAngle, angle));
        }

        private DamageInfo Hit(float amount, in KeystoneContact contact) =>
            new(amount, DamageKind.Collision, contact.Self, contact.SelfMass, contact.SelfVelocity, contact.Point,
                attacker: Host.Ship);
    }
}

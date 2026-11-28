using System;
using Balance;
using Damage;
using Loot;
using Movement;
using Ships.Command;
using Ships.Damage;
using UnityEngine;
using Ships.Presentation;

namespace Ships.Keystones
{
    /// <summary>
    /// The locked ability in a ship's keystone slot, one kit per ship. The ship instantiates its kit
    /// prefab and hands the instance to <see cref="Ships.Movement.MovementController"/>, which steps it
    /// every fixed step with the activation bit. The cooldown runs on the kit's own fixed-step clock,
    /// never <c>Time.time</c>, so pacing replays identically across respawns and arenas. A kit's
    /// effects beyond what its steps push on the ship are state on the kit that other systems observe; the
    /// ship hands it what it owns (contacts, blocked hits, its own shots), and the kit decides what pays; words
    /// it pops go out on <see cref="Popped"/>, which the ship forwards to its word channel. An AI
    /// commander also reads the kit's sight, which can fire it beside the brain's own request. A ship that
    /// should arrive with the kit already running calls <see cref="ActivateOnArrival"/> before its first step.
    /// Design: https://github.com/amindell11/astronomical-home/issues/1028
    /// </summary>
    public abstract class Keystone : MonoBehaviour
    {
        [Tooltip("Seconds from one activation until the next is ready.")]
        [Stat, SerializeField] private float cooldown = 5f;

        [Tooltip("Seconds an AI pilot waits after first engaging a target before it may fire this kit.")]
        [Stat, SerializeField, Min(0f)] private float firstEngageHold;

        [Tooltip("Glyph shown in the keystone instrument.")]
        [SerializeField] private Sprite icon;

        private float clock;
        private float readyAt;
        private float activatedAt = float.NegativeInfinity;
        private int creditedActivation;

        public abstract string DisplayName { get; }
        public Sprite Icon => icon;
        public float Cooldown => cooldown;
        public float FirstEngageHold => firstEngageHold;
        public bool Ready => clock >= readyAt;

        /// <summary>Whether a press now does something: the kit is ready, or it takes one on cooldown.</summary>
        public bool TakesPress => Ready || TakesPressOnCooldown;
        public float CooldownRemaining => Mathf.Max(0f, readyAt - clock);
        public float CooldownPct => cooldown > 0f ? CooldownRemaining / cooldown : 0f;
        public int Activations { get; private set; }

        /// <summary>Presses the kit's press tell plays for: its activations, and any follow-up it takes on cooldown.</summary>
        public virtual int TelledPresses => Activations;

        /// <summary>Whether the kit is holding open a window for a follow-up press, which the keystone gauge shows.</summary>
        public virtual bool FollowUpOpen => false;

        /// <summary>Times the kit has refunded its whole cooldown, which its tell shows.</summary>
        public int Refunds { get; private set; }

        /// <summary>The held keystone mods that land on this kit.</summary>
        public HeldBehaviours Behaviours { get; } = new();
        public int PaidActivations { get; private set; }
        public Rigidbody Body => Host.Body;

        /// <summary>The kit's active state, which its tell shows.</summary>
        public virtual bool Active => false;

        /// <summary>Contacts the kit has spent itself on, which its tell shows.</summary>
        public int Hits { get; private set; }

        public Vector3 LastHitPoint { get; private set; }

        /// <summary>True while the kit wants to touch its target, so the navigator must not steer around it.</summary>
        public virtual bool SeeksContact => false;

        /// <summary>True while other ships' hulls touching this hull count as contacts, not only bumper hits.</summary>
        public virtual bool ContactsByHull => false;

        /// <summary>The kit attacks along the heading the navigator flies, so an aim servo must not yaw the ship.</summary>
        public virtual bool FliesNavigatorHeading => false;

        /// <summary>The kit's autopilot has the throttle: the ship drops the pilot's thrust and strafe, keeping yaw.</summary>
        public virtual bool HoldsThrottle => false;

        /// <summary>How far through its throttle hold the kit is, 0 to 1; 0 while it holds none.</summary>
        public virtual float HoldProgress => 0f;

        public virtual float SpeedScale => 1f;
        public virtual float ThrustScale => 1f;
        public virtual float ManeuverabilityScale => 1f;

        /// <summary>Multiplier on the ship's mass, applied every step.</summary>
        public virtual float MassScale => 1f;

        /// <summary>Whether the hull flies through asteroids this step instead of colliding with them.</summary>
        public virtual bool PassesRocks => false;

        /// <summary>Multiplier on every hit the ship's own shots deal, fixed as each shot leaves.</summary>
        public virtual float ShotDamageScale => 1f;

        /// <summary>The word each of the ship's own shots pops where it hits, fixed as each shot leaves; null for none.</summary>
        public virtual Word? ShotWord => null;

        public event Action<Word, Vector3> Popped;

        /// <summary>The largest scales the kit ever reports, so anything sized once at spawn covers them.</summary>
        public virtual float PeakSpeedScale => 1f;
        public virtual float PeakThrustScale => 1f;

        protected float SinceActivation => clock - activatedAt;
        protected KeystoneHost Host { get; private set; }

        /// <summary>The pilot's turn command this step, -1 to 1; positive turns the nose counter-clockwise.</summary>
        protected float Turn { get; private set; }

        /// <summary>Whether the pilot thrusts or strafes this step.</summary>
        protected bool Thrusting { get; private set; }

        public void Initialize(in KeystoneHost host) => Host = host;

        /// <param name="target">Where the pilot points the ability, as a plane point; null means straight ahead.</param>
        /// <param name="turn">The pilot's turn command, which <see cref="Turn"/> reports.</param>
        /// <param name="thrusting">Whether the pilot thrusts or strafes, which <see cref="Thrusting"/> reports.</param>
        public KeystoneEffect Step(in Kinematics kinematics, float input, float dt, out bool activated,
            float baseMaxSpeed = float.PositiveInfinity, Vector2? target = null, float turn = 0f, bool thrusting = false)
        {
            clock += dt;
            Turn = turn;
            Thrusting = thrusting;
            activated = Ready && input != 0f;
            var effect = activated ? Commit(in kinematics, target) : Idle(in kinematics, input);
            return effect + Sustain(in kinematics, dt, baseMaxSpeed);
        }

        public void ActivateOnArrival(in Kinematics kinematics)
        {
            if (!Ready)
                throw new InvalidOperationException($"{name} is not ready to arrive already running.");
            var effect = Commit(in kinematics, null);
            // A brake takes the throttle on the press and moves the ship on later steps.
            if (effect.Impulse != Vector2.zero || effect.Displacement != Vector2.zero || HoldsThrottle)
                throw new InvalidOperationException(
                    $"{name} cannot arrive already running: its activation moves the ship.");
        }

        private KeystoneEffect Commit(in Kinematics kinematics, Vector2? target)
        {
            readyAt = clock + cooldown;
            activatedAt = clock;
            Activations++;
            return Activate(in kinematics, target);
        }

        public void ResetKeystone()
        {
            clock = 0f;
            readyAt = 0f;
            activatedAt = float.NegativeInfinity;
            Activations = 0;
            PaidActivations = 0;
            Hits = 0;
            Refunds = 0;
            creditedActivation = 0;
            OnReset();
        }

        public virtual bool Blocks(in DamageInfo hit) => false;

        /// <summary>Multiplier the kit puts on a hit it doesn't block.</summary>
        public virtual float DamageScale(in DamageInfo hit) => 1f;

        /// <summary>The kit's sight: whether an engaged AI should fire it now, beside any brain request.</summary>
        public virtual bool ShouldActivate(in KeystoneContext context) => false;

        public virtual void NotifyBlocked(DamageInfo hit) { }

        public virtual void NotifyFired(WeaponSlot slot) { }

        public virtual void OnContact(in KeystoneContact contact) { }

        protected abstract KeystoneEffect Activate(in Kinematics kinematics, Vector2? target);

        protected virtual KeystoneEffect Sustain(in Kinematics kinematics, float dt, float baseMaxSpeed) => default;

        /// <summary>Each step the input does not activate the kit, with that input: pressed on cooldown, or released.</summary>
        protected virtual KeystoneEffect Idle(in Kinematics kinematics, float input) => default;

        protected virtual bool TakesPressOnCooldown => false;

        protected virtual void OnReset() { }

        protected void ShortenCooldown(float secondsFromActivation) =>
            readyAt = Mathf.Min(readyAt, activatedAt + secondsFromActivation);

        protected void RefundCooldown()
        {
            ShortenCooldown(0f);
            Refunds++;
        }

        protected void RestartCooldown() => readyAt = clock + cooldown;

        protected void Pop(Word word, Vector3 at) => Popped?.Invoke(word, at);

        /// <summary>Whether the collider belongs to a ship on another team than the host's.</summary>
        protected bool TryEnemy(Collider collider, out Ship enemy)
        {
            enemy = DamageableColliders.TryGet(collider, out var owner) && owner is DamageController damage ? damage.Ship : null;
            if (!enemy) return false;
            if (!Host.Ship)
                throw new InvalidOperationException($"{name} looks for enemies on a host with no ship.");
            return enemy != Host.Ship && enemy.teamNumber != Host.Ship.teamNumber;
        }

        protected void RecordHit(Vector3 point)
        {
            Hits++;
            LastHitPoint = point;
        }

        protected void CreditActivation()
        {
            if (creditedActivation == Activations) return;
            creditedActivation = Activations;
            PaidActivations++;
        }
    }
}

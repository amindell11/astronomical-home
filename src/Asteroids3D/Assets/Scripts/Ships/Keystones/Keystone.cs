using Balance;
using Damage;
using Movement;
using UnityEngine;

namespace Ships.Keystones
{
    /// <summary>
    /// The locked ability in a ship's keystone slot, one kit per ship. The ship instantiates its kit
    /// prefab and hands the instance to <see cref="Ships.Movement.MovementController"/>, which steps it
    /// every fixed step with the activation bit. The cooldown runs on the kit's own fixed-step clock,
    /// never <c>Time.time</c>, so pacing replays identically across respawns and arenas. A kit's
    /// effects beyond the activation impulse are state on the kit that other systems observe; the
    /// ship hands it what it owns (contacts, blocked hits), and the kit decides what pays. An AI
    /// commander also reads the kit's sight, which can fire it beside the brain's own request.
    /// Design: https://github.com/amindell11/astronomical-home/issues/1028
    /// </summary>
    public abstract class Keystone : MonoBehaviour
    {
        [Tooltip("Seconds from one activation until the next is ready.")]
        [Stat, SerializeField] private float cooldown = 5f;

        private float clock;
        private float readyAt;
        private float activatedAt = float.NegativeInfinity;
        private int creditedActivation;

        public abstract string DisplayName { get; }
        public float Cooldown => cooldown;
        public bool Ready => clock >= readyAt;
        public float CooldownRemaining => Mathf.Max(0f, readyAt - clock);
        public float CooldownPct => cooldown > 0f ? CooldownRemaining / cooldown : 0f;
        public int Activations { get; private set; }
        public int PaidActivations { get; private set; }

        /// <summary>The kit's active state, which its tell shows.</summary>
        public virtual bool Active => false;

        /// <summary>True while the kit wants to touch its target, so the navigator must not steer around it.</summary>
        public virtual bool SeeksContact => false;

        /// <summary>Multipliers on the ship's speed cap and forward thrust this step; 1 leaves them alone.</summary>
        public virtual float SpeedScale => 1f;
        public virtual float ThrustScale => 1f;

        /// <summary>The largest scales the kit ever reports, so anything sized once at spawn covers them.</summary>
        public virtual float PeakSpeedScale => 1f;
        public virtual float PeakThrustScale => 1f;

        protected float SinceActivation => clock - activatedAt;
        protected KeystoneHost Host { get; private set; }

        public void Initialize(in KeystoneHost host) => Host = host;

        /// <param name="target">Where the pilot points the ability, as a plane point; null means straight ahead.</param>
        public KeystoneEffect Step(in Kinematics kinematics, float input, float dt, out bool activated,
            float baseMaxSpeed = float.PositiveInfinity, Vector2? target = null)
        {
            clock += dt;
            activated = Ready && input != 0f;
            var effect = default(KeystoneEffect);
            if (activated)
            {
                readyAt = clock + cooldown;
                activatedAt = clock;
                Activations++;
                effect = Activate(in kinematics, target);
            }
            Sustain(in kinematics, baseMaxSpeed);
            return effect;
        }

        public void ResetKeystone()
        {
            clock = 0f;
            readyAt = 0f;
            activatedAt = float.NegativeInfinity;
            Activations = 0;
            PaidActivations = 0;
            creditedActivation = 0;
        }

        public virtual bool Blocks(in DamageInfo hit) => false;

        /// <summary>The kit's sight: whether an engaged AI should fire it now, beside any brain request.</summary>
        public virtual bool ShouldActivate(in KeystoneContext context) => false;

        public virtual void NotifyBlocked(DamageInfo hit) { }

        public virtual void OnContact(in KeystoneContact contact) { }

        protected abstract KeystoneEffect Activate(in Kinematics kinematics, Vector2? target);

        protected virtual void Sustain(in Kinematics kinematics, float baseMaxSpeed) { }

        protected void CreditActivation()
        {
            if (creditedActivation == Activations) return;
            creditedActivation = Activations;
            PaidActivations++;
        }
    }
}

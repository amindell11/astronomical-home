using Balance;
using Movement;
using Ships.Mods;
using UnityEngine;

namespace Ships.Keystones.Kits
{
    /// <summary>
    /// Nightshade's kit: no impulse, a window in which the ship is concealed, ended early by its own
    /// first shot. Concealment is one flag on the ship's lock channel, raised as an event, so every
    /// lock and seeker on the ship lets go on the step it starts. Projectiles still hit; stealth
    /// hides the ship, it does not dodge, but it raises the cap and thrust like Overdrive so the
    /// ship can leave where it was last seen. It pays when it breaks a missile's track.
    /// Over the last moments of the window the hull tells that the ship is about to show. With Ambush,
    /// the ship's shots hit harder from concealment until a short while after it shows.
    /// </summary>
    public sealed class Stealth : Keystone
    {
        [Tooltip("Seconds the ship stays concealed unless it fires first.")]
        [Stat, SerializeField] private float window = 3f;

        [Tooltip("Seconds before the window ends that the tell plays.")]
        [Stat, SerializeField, Min(0f)] private float tellLead = 0.4f;

        [Tooltip("Shield fraction under which an engaged AI conceals itself.")]
        [Stat, SerializeField] private float sightShield = 0.3f;

        [Tooltip("Multiplier on the speed cap while concealed.")]
        [Stat, SerializeField] private float speedScale = 1.3f;

        [Tooltip("Multiplier on forward thrust while concealed; must lift drag-limited top speed past the raised cap.")]
        [Stat, SerializeField] private float thrustScale = 1.45f;

        [Tooltip("The Ambush keystone mod, whose holding makes the ship's shots hit harder as it leaves stealth.")]
        [SerializeField] private KeystoneMod ambush;

        [Tooltip("With Ambush, seconds after the ship shows during which its shots hit harder.")]
        [Stat, SerializeField, Min(0f)] private float ambushWindow = 1.5f;

        [Tooltip("With Ambush, multiplier on the damage of every shot fired concealed or in the ambush window.")]
        [Stat, SerializeField] private float ambushDamageScale = 3f;

        private bool concealed;
        private float revealedAt = float.NegativeInfinity;

        public override string DisplayName => "Stealth";
        public float Window => window;
        public bool Telling => concealed && SinceActivation >= window - tellLead;
        public override bool Active => concealed;
        public override float SpeedScale => concealed ? speedScale : 1f;
        public override float ThrustScale => concealed ? thrustScale : 1f;
        public override float PeakSpeedScale => speedScale;
        public override float PeakThrustScale => thrustScale;

        // Concealment counts too: the shot that breaks stealth leaves before the reveal it causes.
        public bool Ambushing => Behaviours.Holds(ambush) && (concealed || SinceActivation - revealedAt < ambushWindow);
        public override float ShotDamageScale => Ambushing ? ambushDamageScale : 1f;

        public override bool ShouldActivate(in KeystoneContext context) => context.SelfShieldPct < sightShield;

        public override void NotifyFired()
        {
            if (concealed) Reveal();
        }

        protected override KeystoneEffect Activate(in Kinematics kinematics, Vector2? target)
        {
            if (Host.Lock.Trackers.Count > 0) CreditActivation();
            concealed = true;
            Host.Lock.Conceal();
            return default;
        }

        protected override KeystoneEffect Sustain(in Kinematics kinematics, float dt, float baseMaxSpeed)
        {
            if (concealed && SinceActivation >= window) Reveal();
            return default;
        }

        protected override void OnReset()
        {
            if (concealed) Reveal();
            revealedAt = float.NegativeInfinity;
        }

        private void Reveal()
        {
            concealed = false;
            revealedAt = SinceActivation;
            Host.Lock.Reveal();
        }
    }
}

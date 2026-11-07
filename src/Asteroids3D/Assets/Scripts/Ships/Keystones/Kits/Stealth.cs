using Balance;
using Movement;
using UnityEngine;

namespace Ships.Keystones.Kits
{
    /// <summary>
    /// Nightshade's kit: no impulse, a window in which the ship is concealed, ended early by its own
    /// first shot. Concealment is one flag on the ship's lock channel, raised as an event, so every
    /// lock and seeker on the ship lets go on the step it starts. Projectiles still hit; stealth
    /// hides the ship, it does not dodge, but it raises the cap and thrust like Overdrive so the
    /// ship can leave where it was last seen. It pays when it breaks a missile's track.
    /// </summary>
    public sealed class Stealth : Keystone
    {
        [Tooltip("Seconds the ship stays concealed unless it fires first.")]
        [Stat, SerializeField] private float window = 3f;

        [Tooltip("Shield fraction under which an engaged AI conceals itself.")]
        [Stat, SerializeField] private float sightShield = 0.3f;

        [Tooltip("Multiplier on the speed cap while concealed.")]
        [Stat, SerializeField] private float speedScale = 1.3f;

        [Tooltip("Multiplier on forward thrust while concealed; must lift drag-limited top speed past the raised cap.")]
        [Stat, SerializeField] private float thrustScale = 1.45f;

        private bool concealed;

        public override string DisplayName => "Stealth";
        public float Window => window;
        public override bool Active => concealed;
        public override float SpeedScale => concealed ? speedScale : 1f;
        public override float ThrustScale => concealed ? thrustScale : 1f;
        public override float PeakSpeedScale => speedScale;
        public override float PeakThrustScale => thrustScale;

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

        protected override void Sustain(in Kinematics kinematics, float baseMaxSpeed)
        {
            if (concealed && SinceActivation >= window) Reveal();
        }

        protected override void OnReset()
        {
            if (concealed) Reveal();
        }

        private void Reveal()
        {
            concealed = false;
            Host.Lock.Reveal();
        }
    }
}

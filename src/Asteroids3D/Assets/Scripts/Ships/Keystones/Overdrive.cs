using Balance;
using Movement;
using UnityEngine;

namespace Ships.Keystones
{
    /// <summary>
    /// Valis's kit: no impulse, a window in which the ship's speed cap and forward thrust are raised.
    /// The ship reads the scales live (rigidbody cap, thrust, and the dynamics the solver plans with);
    /// after the window the cap bleeds back down, so the speed it built is the kit's cost to shed.
    /// </summary>
    public sealed class Overdrive : Keystone
    {
        [Tooltip("Seconds after activation during which the cap and thrust stay raised.")]
        [Stat, SerializeField] private float window = 3f;

        [Tooltip("Multiplier on the speed cap during the window.")]
        [Stat, SerializeField] private float speedScale = 1.35f;

        [Tooltip("Multiplier on forward thrust during the window; must lift drag-limited top speed past the raised cap.")]
        [Stat, SerializeField] private float thrustScale = 1.5f;

        [Tooltip("Fraction of the cap the navigator must ask for before an AI fires.")]
        [Stat, SerializeField] private float sightSaturation = 0.8f;

        public override string DisplayName => "Overdrive";
        public float Window => window;
        public override bool Active => SinceActivation < window;
        public override float SpeedScale => Active ? speedScale : 1f;
        public override float ThrustScale => Active ? thrustScale : 1f;
        public override float PeakSpeedScale => speedScale;
        public override float PeakThrustScale => thrustScale;

        // Saturated: the navigator asks for (nearly) all the speed the cap allows.
        public override bool ShouldActivate(in KeystoneContext context)
        {
            var desired = context.DesiredVelocity;
            return !float.IsNaN(desired.x) && desired.magnitude >= sightSaturation * context.SelfMaxSpeed;
        }

        protected override Vector2 Activate(in Kinematics kinematics) => Vector2.zero;

        protected override void Sustain(in Kinematics kinematics, float baseMaxSpeed)
        {
            if (Active && kinematics.Speed > baseMaxSpeed) CreditActivation();
        }
    }
}

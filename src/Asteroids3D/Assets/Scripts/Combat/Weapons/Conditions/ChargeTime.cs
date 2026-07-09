using System;
using Combat.Weapons;
using UnityEngine;

namespace Combat.Conditions
{
    /// <summary>
    /// What a charge gauge renders: the read-only, event-driven face of <see cref="ChargeTime"/>.
    /// (Co-located with its sole implementer; C# disallows implementing an interface nested
    /// in the implementing class itself.)
    /// </summary>
    public interface IChargeReadout : IWeaponReadout
    {
        /// <summary>Current charge in [0, 1].</summary>
        float ChargePct { get; }
        event Action<float> OnChargeChanged;
    }

    /// <summary>
    /// Charge-up gate for hold-to-charge weapons. The owning weapon feeds it raw trigger state
    /// each physics step via <see cref="HandleTrigger"/>, which implements the shared charge
    /// semantics: charge accumulates while the trigger is held, the weapon fires on release at
    /// or above <see cref="minChargeToFire"/> (or automatically the moment full charge is
    /// reached, when <see cref="autoFireAtFull"/> — which is also how the AI fires charge
    /// weapons, since it holds rather than timing a release). An unfired release drops the
    /// charge; firing consumes it (<see cref="ProcessFire"/>).
    /// </summary>
    public class ChargeTime : WeaponCondition, IChargeReadout
    {
        [Header("Charge System")]
        [Tooltip("Seconds of holding the trigger to reach full charge.")]
        [SerializeField, Min(0.01f)] private float chargeTime = 1f;

        [Tooltip("Fraction of full charge required to fire on release (1 = full charge only).")]
        [SerializeField, Range(0f, 1f)] private float minChargeToFire = 0.3f;

        [Tooltip("Fire automatically the moment full charge is reached while still holding.")]
        [SerializeField] private bool autoFireAtFull = true;

        private bool wasHeld;

        public event Action<float> OnChargeChanged;

        public float ChargePct { get; private set; }

        public float FullChargeTime => chargeTime;

        /// <summary>
        /// Advances the charge by one trigger step of <paramref name="dt"/> seconds and returns
        /// whether the weapon should attempt to fire this step.
        /// </summary>
        public bool HandleTrigger(bool held, float dt)
        {
            var releaseEdge = wasHeld && !held;
            wasHeld = held;

            if (held)
            {
                SetCharge(Mathf.Min(1f, ChargePct + dt / chargeTime));
                return autoFireAtFull && ChargePct >= 1f;
            }

            if (releaseEdge && ChargePct >= minChargeToFire)
                return true;

            // Unfired charge drains on release.
            if (ChargePct > 0f)
                SetCharge(0f);
            return false;
        }

        public override bool CanFire()
        {
            return ChargePct >= minChargeToFire;
        }

        public override void ProcessFire()
        {
            SetCharge(0f);
        }

        public override void Reset()
        {
            wasHeld = false;
            SetCharge(0f);
        }

        /// <summary>
        /// Configures the charge curve at runtime (weapon tuning / tests) and resets to empty.
        /// Serialized fields act as the authored inspector defaults.
        /// </summary>
        public void Configure(float chargeTime, float minChargeToFire, bool autoFireAtFull)
        {
            this.chargeTime = chargeTime;
            this.minChargeToFire = minChargeToFire;
            this.autoFireAtFull = autoFireAtFull;
            Reset();
        }

        private void SetCharge(float value)
        {
            // Exact comparison on purpose: this only suppresses true no-ops (e.g. re-clamping at
            // full every step). An approximate check here swallows the final ~0.9999999 → 1.0
            // step of the accumulation, pinning the charge just below full so it can never fire.
            if (value == ChargePct) return;
            ChargePct = value;
            OnChargeChanged?.Invoke(ChargePct);
        }
    }
}

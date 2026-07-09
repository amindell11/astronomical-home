using System;
using Combat.Weapons;
using UnityEngine;

namespace Combat.Conditions
{
    /// <summary>
    /// What a heat gauge renders: the read-only, event-driven face of <see cref="Heat"/>.
    /// (Co-located with its sole implementer; C# disallows implementing an interface nested
    /// in the implementing class itself.)
    /// </summary>
    public interface IHeatReadout : IWeaponReadout
    {
        float HeatPct { get; }
        event Action<float, float> OnHeatChanged;
        event Action OnOverheat;
        event Action OnCooldownStart;
    }

    public class Heat : WeaponCondition, IHeatReadout
    {
        [Header("Heat System")]
        [SerializeField] private float maxHeat = 100f;
        [SerializeField] private float heatPerShot = 25f;
        [SerializeField] private float coolingRate = 50f; // units per second
        [SerializeField] private float coolDownDelay = 0.5f; // seconds before cooling starts after a normal shot
        [SerializeField] private float overheatPenaltyTime = 1.5f; // seconds before cooling starts after overheating

        private float clock;                // internal time base, advanced by Tick(dt)
        private float lastShotTime = -100f; // Initialize to allow immediate firing

        public event Action OnOverheat;
        public event Action OnCooldownStart;
        public event Action<float, float> OnHeatChanged;

        public float CurrentHeat { get; private set; }
        public float MaxHeat => maxHeat;
        public float HeatPerShot => heatPerShot;
        public float HeatPct => maxHeat > 0f ? CurrentHeat / maxHeat : 0f;
        public bool Overheated { get; private set; }

        private void Update() => Tick(Time.deltaTime);

        /// <summary>
        /// Advances cooling by <paramref name="dt"/> seconds against an internal clock.
        /// Production drives this each frame with <c>Time.deltaTime</c> (via <see cref="Update"/>);
        /// tests can drive it directly for deterministic, zero-wall-time coverage.
        /// </summary>
        public void Tick(float dt)
        {
            clock += dt;

            if (CurrentHeat <= 0)
            {
                Overheated = false;
                return;
            }

            var delay = Overheated ? overheatPenaltyTime : coolDownDelay;
            if (clock <= lastShotTime + delay) return;

            var previousHeat = CurrentHeat;
            CurrentHeat -= coolingRate * dt;
            CurrentHeat = Mathf.Max(0, CurrentHeat);
            PublishHeatChangedIfNeeded(previousHeat);

            // Hysteresis: require cooling below (maxHeat - heatPerShot) to exit overheat, preventing a
            // flicker loop where the weapon re-fires the instant heat cools to ~99.99%.
            if (!Overheated || CurrentHeat >= maxHeat - heatPerShot) return;
            Overheated = false;
            OnCooldownStart?.Invoke();
        }

        public override bool CanFire()
        {
            return !Overheated;
        }

        public bool WouldOverheatOnNextShot(float extraHeatMargin = 0f)
        {
            return CurrentHeat + heatPerShot + extraHeatMargin > maxHeat;
        }

        public override void ProcessFire()
        {
            var previousHeat = CurrentHeat;
            CurrentHeat += heatPerShot;
            lastShotTime = clock;
            CurrentHeat = Mathf.Min(CurrentHeat, maxHeat);
            PublishHeatChangedIfNeeded(previousHeat);

            if (!(CurrentHeat >= maxHeat) || Overheated) return;
            Overheated = true;
            OnOverheat?.Invoke();
        }

        public override void Reset()
        {
            var previousHeat = CurrentHeat;
            CurrentHeat = 0f;
            clock = 0f;
            lastShotTime = -100f;
            Overheated = false;
            PublishHeatChangedIfNeeded(previousHeat);
        }

        /// <summary>
        /// Configures the heat curve at runtime (weapon tuning / upgrades / difficulty) and resets
        /// to a cold state. Serialized fields act as the authored inspector defaults.
        /// </summary>
        public void Configure(float maxHeat, float heatPerShot, float coolingRate,
                              float coolDownDelay, float overheatPenaltyTime)
        {
            this.maxHeat = maxHeat;
            this.heatPerShot = heatPerShot;
            this.coolingRate = coolingRate;
            this.coolDownDelay = coolDownDelay;
            this.overheatPenaltyTime = overheatPenaltyTime;
            Reset();
        }

        private void PublishHeatChangedIfNeeded(float previousHeat)
        {
            if (Mathf.Approximately(previousHeat, CurrentHeat)) return;
            OnHeatChanged?.Invoke(CurrentHeat, maxHeat);
        }
    }
}

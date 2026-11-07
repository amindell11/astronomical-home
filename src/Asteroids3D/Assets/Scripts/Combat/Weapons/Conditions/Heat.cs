using Balance;
using System;
using Combat.Weapons;
using Combat.Weapons.Mods;
using UnityEngine;

namespace Combat.Weapons.Conditions
{
    /// <summary>
    /// What a heat gauge renders: the read-only, event-driven face of <see cref="Heat"/>.
    /// (Co-located with its sole implementer; C# disallows implementing an interface nested
    /// in the implementing class itself.)
    /// </summary>
    public interface IHeatReadout : IWeaponReadout
    {
        float HeatPct { get; }
        bool Overheated { get; }
        event Action<float, float> OnHeatChanged;
        event Action OnOverheat;
        event Action OnCooldownStart;
    }

    public class Heat : WeaponCondition, IHeatReadout
    {
        [Header("Heat System")]
        [Stat, SerializeField] private float maxHeat = 100f;
        [Stat, SerializeField] private float heatPerShot = 25f;
        [Tooltip("Heat units shed per second while cooling.")]
        [Stat, SerializeField] private float coolingRate = 50f;
        [Tooltip("Seconds before cooling starts after a normal shot.")]
        [Stat, SerializeField] private float coolDownDelay = 0.5f;
        [Tooltip("Seconds before cooling starts after overheating.")]
        [Stat, SerializeField] private float overheatPenaltyTime = 1.5f;

        private float clock;                // internal time base, advanced by Tick(dt)
        private float lastShotTime = -100f; // Initialize to allow immediate firing

        public event Action OnOverheat;
        public event Action OnCooldownStart;
        public event Action<float, float> OnHeatChanged;

        public float CurrentHeat { get; private set; }
        public float MaxHeat => mods.Apply(WeaponModField.HeatCapacity, maxHeat);
        public float HeatPerShot => heatPerShot;
        public float HeatPct => MaxHeat > 0f ? CurrentHeat / MaxHeat : 0f;
        public bool Overheated { get; private set; }

        private float CoolingRate => mods.Apply(WeaponModField.Cooling, coolingRate);

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
            if (clock < lastShotTime + delay + HalfStep) return;

            var previousHeat = CurrentHeat;
            CurrentHeat -= CoolingRate * dt;
            if (CurrentHeat <= CoolingRate * HalfStep) CurrentHeat = 0f;
            PublishHeatChangedIfNeeded(previousHeat);

            // Full lockout by design: partial cooling never re-enables fire.
            if (!Overheated || CurrentHeat > 0f) return;
            Overheated = false;
            OnCooldownStart?.Invoke();
        }

        public override bool CanFire()
        {
            return !Overheated;
        }

        private bool WouldBeOverheated(float heat) => heat >= MaxHeat;

        public bool WouldOverheatOnNextShot(float extraHeatMargin = 0f)
        {
            return WouldBeOverheated(CurrentHeat + heatPerShot + extraHeatMargin);
        }

        /// <summary>Null when cooling between shots keeps pace, so continuous fire never overheats.</summary>
        public int? ShotsToOverheat(float secondsBetweenShots)
        {
            if (heatPerShot >= MaxHeat) return 1;
            var netHeatPerShot = heatPerShot - CoolingRate * Mathf.Max(0f, secondsBetweenShots - coolDownDelay);
            if (netHeatPerShot <= 0f) return null;
            return 1 + Mathf.CeilToInt((MaxHeat - heatPerShot) / netHeatPerShot);
        }

        public override void ProcessFire()
        {
            var previousHeat = CurrentHeat;
            CurrentHeat += heatPerShot;
            lastShotTime = clock;
            CurrentHeat = Mathf.Min(CurrentHeat, MaxHeat);
            PublishHeatChangedIfNeeded(previousHeat);

            if (!WouldBeOverheated(CurrentHeat) || Overheated) return;
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

        // The gauge rescales to the new capacity even though the heat itself did not move.
        protected override void OnModsChanged() => OnHeatChanged?.Invoke(CurrentHeat, MaxHeat);

        private void PublishHeatChangedIfNeeded(float previousHeat)
        {
            if (Mathf.Approximately(previousHeat, CurrentHeat)) return;
            OnHeatChanged?.Invoke(CurrentHeat, MaxHeat);
        }
    }
}

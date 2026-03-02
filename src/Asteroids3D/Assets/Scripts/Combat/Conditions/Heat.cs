using System;
using UnityEngine;

namespace Combat.Conditions
{
    public class Heat : WeaponCondition
    {
        [Header("Heat System")]
        [SerializeField] private float maxHeat = 100f;
        [SerializeField] private float heatPerShot = 25f;
        [SerializeField] private float coolingRate = 50f; // units per second
        [SerializeField] private float coolDownDelay = 0.5f; // seconds before cooling starts after a normal shot
        [SerializeField] private float overheatPenaltyTime = 1.5f; // seconds before cooling starts after overheating

        private float lastShotTime = -100f; // Initialize to allow immediate firing

        // Events
        public event Action OnOverheat;
        public event Action OnCooldownStart;

        public float CurrentHeat { get; private set; }
        public float MaxHeat => maxHeat;
        public float HeatPct => CurrentHeat / maxHeat;
        public bool Overheated { get; private set; }

        private void Update()
        {
            if (CurrentHeat <= 0)
            {
                Overheated = false;
                return;
            }

            var wasOverheatedBefore = Overheated;
            var delay = wasOverheatedBefore ? overheatPenaltyTime : coolDownDelay;

            if (!(Time.time > lastShotTime + delay)) return;
            
            CurrentHeat -= coolingRate * Time.deltaTime;
            CurrentHeat = Mathf.Max(0, CurrentHeat);
            
            // Add hysteresis: require cooling below (maxHeat - heatPerShot) to exit overheat
            // This prevents flicker loop where weapon fires immediately when cooling to 99.99% heat
            if (!Overheated || !(CurrentHeat < maxHeat - heatPerShot)) return;
            Overheated = false;
            OnCooldownStart?.Invoke();
        }

        public override bool CanFire()
        {
            return !Overheated;
        }

        public bool WouldOverheatOnNextShot(float extraHeatMargin = 0f)
        {
            return (CurrentHeat + heatPerShot + extraHeatMargin) > maxHeat;
        }

        public override void ProcessFire()
        {
            CurrentHeat += heatPerShot;
            lastShotTime = Time.time;
            CurrentHeat = Mathf.Min(CurrentHeat, maxHeat);

            if (!(CurrentHeat >= maxHeat) || Overheated) return;
            Overheated = true;
            OnOverheat?.Invoke();
        }

        public override void Reset()
        {
            CurrentHeat = 0f;
            lastShotTime = -100f;
            Overheated = false;
        }
    }
}

using System;
using UnityEngine;

namespace Ships.Weapons.Conditions
{
    public class Heat : WeaponCondition
    {
        [Header("Heat System")]
        [SerializeField] private float maxHeat = 100f;
        [SerializeField] private float heatPerShot = 25f;
        [SerializeField] private float coolingRate = 50f; // units per second
        [SerializeField] private float coolDownDelay = 0.5f; // seconds before cooling starts after a normal shot
        [SerializeField] private float overheatPenaltyTime = 1.5f; // seconds before cooling starts after overheating

        private float _lastShotTime = -100f; // Initialize to allow immediate firing

        // Events
        public event Action OnOverheat;
        public event Action OnCooldownStart;

        public float CurrentHeat { get; private set; }
        public float MaxHeat => maxHeat;
        public float HeatPct => CurrentHeat / maxHeat;
        public bool Overheated => CurrentHeat >= maxHeat;
        
        private void Update()
        {
            if (CurrentHeat <= 0) return;

            bool wasOverheatedBefore = Overheated;
            float delay = wasOverheatedBefore ? overheatPenaltyTime : coolDownDelay;

            if (!(Time.time > _lastShotTime + delay)) return;
            
            CurrentHeat -= coolingRate * Time.deltaTime;
            CurrentHeat = Mathf.Max(0, CurrentHeat);
            
            bool isOverheatedNow = Overheated;
            if (wasOverheatedBefore && !isOverheatedNow)
            {
                OnCooldownStart?.Invoke();
            }
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
            _lastShotTime = Time.time;
            CurrentHeat = Mathf.Min(CurrentHeat, maxHeat);
            
            if (Overheated)
                OnOverheat?.Invoke();
        }

        public override void Reset()
        {
            CurrentHeat = 0f;
            _lastShotTime = -100f;
        }
    }
}

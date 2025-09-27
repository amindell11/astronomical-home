using System;
using UnityEngine;

namespace Weapons
{
    /// <summary>
    /// Concrete weapon that fires pooled <see cref="LaserProjectile"/> instances.
    /// All common launcher logic lives in <see cref="LauncherBase{TProj}"/>.
    /// This weapon uses a heat system for ammo.
    /// </summary>
    public class LaserGun : LauncherBase<LaserProjectile>
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

        public float CurrentHeat { get; private set; } = 0f;

        public float MaxHeat => maxHeat;
        public float HeatPerShot => heatPerShot;
        public float HeatPct => CurrentHeat / maxHeat;
        public float ProjectileSpeed => projectilePrefab.LaserSpeed;
        public bool Overheated => CurrentHeat >= maxHeat;

    
        void Start()
        {
            Reset();
        }

        private void Update()
        {
            if (CurrentHeat <= 0) return;

            bool wasOverheatedBefore = Overheated;
            float delay = wasOverheatedBefore ? overheatPenaltyTime : coolDownDelay;

            if (!(Time.time > lastShotTime + delay)) return;
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
            // Check base for cooldown, then check for heat.
            return base.CanFire() && !Overheated;
        }

        public override ProjectileBase Fire()
        {
            var proj = base.Fire();
            if (!proj) return null;
            
            CurrentHeat += heatPerShot;
            lastShotTime = Time.time;
            CurrentHeat = Mathf.Min(CurrentHeat, maxHeat);
            
            if (Overheated)
                OnOverheat?.Invoke();
            
            return proj;
        }

        public override void Reset()
        {
            CurrentHeat = 0f;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            // Draw heat bar
            if (Application.isPlaying && transform.parent)
            {
                Vector3 position = transform.parent.position + transform.parent.right * 1.5f;
                float heatRatio = CurrentHeat / maxHeat;
            
                UnityEditor.Handles.Label(position + Vector3.up * 1.2f, $"Heat: {CurrentHeat:F0}/{maxHeat:F0}");

                Vector3 barStart = position;
                Vector3 barEnd = position + Vector3.up * 1.0f;
            
                // Background
                UnityEditor.Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                UnityEditor.Handles.DrawAAPolyLine(3f, barStart, barEnd);

                // Foreground
                if (heatRatio > 0)
                {
                    UnityEditor.Handles.color = Color.Lerp(Color.cyan, Color.red, heatRatio);
                    UnityEditor.Handles.DrawAAPolyLine(3f, barStart, Vector3.Lerp(barStart, barEnd, heatRatio));
                }
            }
        }
#endif
    }
} 
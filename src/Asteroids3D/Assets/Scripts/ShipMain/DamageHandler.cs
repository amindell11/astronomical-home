using System;
using Damage;
using UnityEditor;
using UnityEngine;

namespace ShipMain
{
    [RequireComponent(typeof(Movement))]
    public class DamageHandler : MonoBehaviour, IDamageable
    {
        // ------ Events ------
        public event Action<float,float, float> OnShieldChanged;   // current, previous, max
        public event Action<float,float, float> OnHealthChanged;   // current, previous, max
        public event Action<int>         OnLivesChanged;    // remaining lives
        public event Action<float, Vector3> OnDamaged;      // dmg, hitPoint
        public event Action<float, Vector3> OnShieldDamaged; // dmg, hitPoint when shield absorbs
        public event Action<Ship, Ship> OnDeath; // Passes the victim and killer Ship components

        public float maxHealth;
        public float maxShield;
        public int   startingLives;
        public float shieldRegenDelay;
        public float shieldRegenRate;

        // ------ State ------
        private float lastDamageTime;
        public Ship  LastAttacker {get; private set;}
        private Ship myShip;

        // --- Spawn Invulnerability --------------------------------------
        private float invulnerableUntil = 0f;

        /// <summary>
        /// Indicates whether the ship is currently invulnerable (takes no damage).
        /// </summary>
        public bool IsInvulnerable { get; private set; } = false;

        public float CurrentHealth { get; private set; }

        public float CurrentShield { get; private set; }

        public int   Lives { get; private set; }

        public float HealthPct => CurrentHealth / maxHealth;
        public float ShieldPct => CurrentShield / maxShield;
        public float RegenWait => Time.time - lastDamageTime - shieldRegenDelay;
        public bool RegenUp => RegenWait >= 0;

        public float InvulTimeLeft => invulnerableUntil - Time.time;

        private void Awake()
        {
            CurrentHealth = maxHealth;
            CurrentShield = maxShield;
            Lives         = startingLives;
            myShip = GetComponent<Ship>();
            BroadcastState();
        }

        private void Update()
        {
            IsInvulnerable = IsInvulnerable && InvulTimeLeft >= 0; 
            if (CurrentShield < maxShield && RegenUp)
                RegenShield();
        }

        private void RegenShield()
        {
            float regen = shieldRegenRate * Time.deltaTime;
            var oldShield = CurrentShield;
            CurrentShield = Mathf.Min(CurrentShield + regen, maxShield);
            if(!Mathf.Approximately(CurrentShield, oldShield))
                OnShieldChanged?.Invoke(CurrentShield, oldShield, maxShield);
        }

        public void TakeDamage(float damage, float projectileMass, Vector3 projectileVelocity, Vector3 hitPoint, GameObject attacker)
        {
            if (damage <= 0 || IsInvulnerable) return;
            lastDamageTime = Time.time;
            var attackShip = attacker.GetComponentInParent<Ship>();
            if (attackShip)
                LastAttacker = attackShip;
            if (CurrentShield > 0f)
            {
                var absorbed = ApplyShieldDamage(damage, hitPoint);
                Ship.BroadcastShipDamaged(myShip, attacker, absorbed);
                return;
            }

            var healthDmg = ApplyHealthDamage(damage, hitPoint);
            Ship.BroadcastShipDamaged(myShip, attacker, healthDmg);
        
            if (CurrentHealth <= 0f)
            {
                LoseLife();
            }
        }

        private float ApplyHealthDamage(float damage, Vector3 hitPoint)
        {
            var oldHealth = CurrentHealth;
            CurrentHealth = Mathf.Max(CurrentHealth - damage, 0f);
            float actualHealthDamage = oldHealth - CurrentHealth;
            OnHealthChanged?.Invoke(CurrentHealth, oldHealth, maxHealth);
            OnDamaged?.Invoke(actualHealthDamage, hitPoint);
            return actualHealthDamage;
        }

        private float ApplyShieldDamage(float damage, Vector3 hitPoint)
        {
            float absorbed = Mathf.Min(damage, CurrentShield);
            var oldShield = CurrentShield;
            CurrentShield -= absorbed;
            OnShieldChanged?.Invoke(CurrentShield, oldShield, maxShield);
            OnShieldDamaged?.Invoke(absorbed, hitPoint);
            return absorbed;
        }

        private void LoseLife()
        {
            Lives = Mathf.Max(Lives - 1, 0);
            OnLivesChanged?.Invoke(Lives);
            if (Lives > 0)
                ResetDamageState();
            else
                BroadcastDeath();
        }
    
        /// <summary>
        /// Grant temporary invulnerability for the given duration (seconds).
        /// </summary>
        /// <param name="duration">Duration in seconds. Pass 0 or negative to clear immediately.</param>
        public void SetInvulnerability(float duration)
        {
            if (duration <= 0f)
            {
                IsInvulnerable = false;
                invulnerableUntil = 0f;
            }
            else
            {
                IsInvulnerable = true;
                invulnerableUntil = Time.time + duration;
            }
        }
    
        public void ResetDamageState()
        {
            CurrentHealth = maxHealth;
            CurrentShield = maxShield;
            Lives         = startingLives;
            BroadcastState();
            SetInvulnerability(0f);
        }
        private void BroadcastState()
        {
            OnShieldChanged?.Invoke(CurrentShield, CurrentShield, maxShield);
            OnHealthChanged?.Invoke(CurrentHealth, CurrentHealth, maxHealth);
            OnLivesChanged?.Invoke(Lives);
        }
        private void BroadcastDeath()
        {
            OnDeath?.Invoke(myShip, LastAttacker);
        }
        public void PopulateSettings(Settings s)
        {
            if (!s) return;

            maxHealth       = s.maxHealth;
            maxShield       = s.maxShield;
            startingLives   = s.startingLives;
            shieldRegenDelay= s.shieldRegenDelay;
            shieldRegenRate = s.shieldRegenRate;

            ResetDamageState();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            /* ------------------- Configurable offsets ------------------- */
            const float baseOffset   = 2f;   // distance from ship to first element (shield bar)
            const float barSpacing   = 0.25f; // gap between shield and health bars
            const float textSpacing  = .75f;  // gap between each text line
            const float barWidth     = 3.5f;
            const float barHeight    = 0.25f;
            const float barDepth     = 0.1f; // thickness along GamePlane.Normal

            /* ------------------- Position chain ------------------------- */
            // Start just "above" the ship along the forward axis
            Vector3 shieldBarPos  = transform.position + GamePlane.Forward * baseOffset;
            Vector3 healthBarPos  = shieldBarPos   + GamePlane.Forward * barSpacing;
            Vector3 shieldTextPos = healthBarPos   + GamePlane.Forward * (barSpacing*2+textSpacing*2);
            Vector3 healthTextPos = shieldTextPos  + GamePlane.Forward * textSpacing;

            /* ------------------- Draw Bars ------------------------------ */
            Vector3 barSize = GamePlane.Right * barWidth + GamePlane.Forward * barHeight + GamePlane.Normal * barDepth;

            // Shield Bar (background + fill)
            Gizmos.color = Color.gray; // background
            Gizmos.DrawCube(shieldBarPos, barSize);
            if (maxShield > 0)
            {
                float shieldPercent = CurrentShield / maxShield;
                Gizmos.color = Color.cyan; // fill
                Vector3 fillPos  = shieldBarPos - GamePlane.Right * (barWidth * (1f - shieldPercent) * 0.5f);
                Vector3 fillSize = GamePlane.Right * (barWidth * shieldPercent) + GamePlane.Forward * barHeight + GamePlane.Normal * barDepth;
                Gizmos.DrawCube(fillPos, fillSize);
            }

            // Health Bar (background + fill)
            Gizmos.color = Color.red; // background
            Gizmos.DrawCube(healthBarPos, barSize);
            if (maxHealth > 0)
            {
                float healthPercent = CurrentHealth / maxHealth;
                Gizmos.color = Color.green; // fill
                Vector3 fillPos  = healthBarPos - GamePlane.Right * (barWidth * (1f - healthPercent) * 0.5f);
                Vector3 fillSize = GamePlane.Right * (barWidth * healthPercent) + GamePlane.Forward * barHeight + GamePlane.Normal * barDepth;
                Gizmos.DrawCube(fillPos, fillSize);
            }

            /* ------------------- Draw Text ------------------------------ */
            string shieldText = $"Shield: {CurrentShield:F1}/{maxShield:F1}";
            string healthText = $"Health: {CurrentHealth:F1}/{maxHealth:F1}";

            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.fontSize = 12;
            style.alignment = TextAnchor.MiddleCenter;

            Handles.Label(shieldTextPos, shieldText, style);
            Handles.Label(healthTextPos, healthText, style);
        }
#endif
    }
} 
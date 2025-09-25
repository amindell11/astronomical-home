using System;
using Damage;
using Game;
using UnityEditor;
using UnityEngine;

namespace Ships
{
    public class Damage : MonoBehaviour, IDamageable
    {
        public event Action<float, Vector3> OnDamaged; // dmg, hitPoint
        public event Action<Ship, Ship> OnDeath; // Passes the victim and killer Ship components

        public float maxHealth = 100f;
        public float maxShield = 100f;
        public float shieldRegenDelay = 3f;
        public float shieldRegenRate = 25f;

        public DamageResource Health { get; private set; }
        public RegeneratingDamageResource Shield { get; private set; }
        
        public Ship LastAttacker {get; private set;}
        private Ship myShip;

        private float invulnerableUntil = 0f;
        public bool IsInvulnerable { get; private set; } = false;
        
        public float InvulTimeLeft => invulnerableUntil - Time.time;

        private void Awake()
        {
            myShip = GetComponent<Ship>();
            Health = new DamageResource(maxHealth);
            Shield = new RegeneratingDamageResource(maxShield, shieldRegenRate, shieldRegenDelay);
        }

        private void Update()
        {
            IsInvulnerable = IsInvulnerable && InvulTimeLeft >= 0; 
            Shield.Update(Time.deltaTime);
        }

        public void TakeDamage(float damage, float hitMass, Vector3 hitVelocity, Vector3 hitPoint, GameObject attacker)
        { 
            if (damage <= 0 || IsInvulnerable) return; 
            
            UpdateAttacker(attacker);
            
            float damageRemaining = Shield.ApplyDamage(damage);
            float appliedDamage = damage - damageRemaining;
            
            if (damageRemaining > 0)
            {
                float healthDamage = Health.ApplyDamage(damageRemaining);
                appliedDamage += healthDamage;
            }

            if (appliedDamage > 0)
            {
                OnDamaged?.Invoke(appliedDamage, hitPoint);
            }

            if (Health.CurrentValue <= 0f) 
            {
                BroadcastDeath();
            }
        }

        private void UpdateAttacker(GameObject attacker)
        {
            if (!attacker) return;
            var attackShip = attacker.GetComponentInParent<Ship>();
            if (attackShip) LastAttacker = attackShip;
        }
        
        private void BroadcastDeath()
        {
            OnDeath?.Invoke(myShip, LastAttacker);
        }
   
        /// <summary>
        /// Grant temporary invulnerability for the given duration (seconds).
        /// </summary>
        /// <param name="duration">Duration in seconds. Pass 0 or negative to clear immediately.</param>
        public void SetInvulnerability(float duration)
        {
            float clamped = Mathf.Max(duration, 0f);
            invulnerableUntil = Time.time + clamped;
            IsInvulnerable = clamped > 0f;
        }
    
        public void ResetDamageState()
        {
            Health.Reset();
            Shield.Reset();
            SetInvulnerability(0f);
        }
        
        public void PopulateSettings(Settings s)
        {
            if (!s) return;
            
            maxHealth       = s.maxHealth;
            maxShield       = s.maxShield;
            shieldRegenDelay= s.shieldRegenDelay;
            shieldRegenRate = s.shieldRegenRate;

            Health.Configure(maxHealth);
            Shield.Configure(maxShield, shieldRegenRate, shieldRegenDelay);

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
                float shieldPercent = Shield.Pct;
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
                float healthPercent = Health.Pct;
                Gizmos.color = Color.green; // fill
                Vector3 fillPos  = healthBarPos - GamePlane.Right * (barWidth * (1f - healthPercent) * 0.5f);
                Vector3 fillSize = GamePlane.Right * (barWidth * healthPercent) + GamePlane.Forward * barHeight + GamePlane.Normal * barDepth;
                Gizmos.DrawCube(fillPos, fillSize);
            }

            /* ------------------- Draw Text ------------------------------ */
            string shieldText = $"Shield: {Shield.CurrentValue:F1}/{maxShield:F1}";
            string healthText = $"Health: {Health.CurrentValue:F1}/{maxHealth:F1}";

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

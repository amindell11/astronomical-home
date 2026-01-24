using System;
using Damage;
using UnityEngine;

namespace Ships.Damage
{
    public partial class DamageController : MonoBehaviour, IDamageable
    {
        public event Action<float, Vector3> OnDamaged; // dmg, hitPoint
        public event Action<Ship, Ship> OnDeath; // Passes the victim and killer Ship components

        public float maxHealth = 100f;
        public float maxShield = 100f;
        public float shieldRegenDelay = 3f;
        public float shieldRegenRate = 25f;

        public Resource Health { get; private set; }
        public RegenResource Shield { get; private set; }
        
        public Ship LastAttacker {get; private set;}
        private Ship myShip;

        private float invulnerableUntil;
        public bool IsInvulnerable { get; private set; }
        
        public float InvulTimeLeft => invulnerableUntil - Time.time;

        private void Awake()
        {
            myShip = GetComponent<Ship>();
            Health = new Resource(maxHealth);
            Shield = new RegenResource(maxShield, shieldRegenRate, shieldRegenDelay);
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
            
            float appliedDamage;
            
            // If shields are already depleted, damage goes directly to health
            if (Shield.CurrentValue <= 0)
            {
                appliedDamage = Health.ApplyDamage(damage);
            }
            else
            {
                // Shields absorb damage; excess from a single hit is discarded (single-hit rule)
                appliedDamage = Shield.ApplyDamage(damage);
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
            var clamped = Mathf.Max(duration, 0f);
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
            
            Health ??= new Resource(maxHealth);
            Shield ??= new RegenResource(maxShield, shieldRegenRate, shieldRegenDelay);

            maxHealth       = s.maxHealth;
            maxShield       = s.maxShield;
            shieldRegenDelay= s.shieldRegenDelay;
            shieldRegenRate = s.shieldRegenRate;

            Health.Configure(maxHealth);
            Shield.Configure(maxShield, shieldRegenRate, shieldRegenDelay);

            ResetDamageState();
        }
    }
} 

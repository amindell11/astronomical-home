using System;
using Damage;
using UnityEngine;

namespace Ships.Damage
{
    public partial class DamageController : MonoBehaviour, IDamageable, IDamageEvents
    {
        public event Action<float, Vector3> OnDamaged; // dmg, hitPoint
        public event Action<ShipId, ShipId> OnDeath; // victimId, killerId

        public float maxHealth = 100f;
        public float maxShield = 100f;
        public float shieldRegenDelay = 3f;
        public float shieldRegenRate = 25f;

        public Resource Health { get; private set; }
        public RegenResource Shield { get; private set; }
        
        public ShipId LastAttackerId { get; private set; } = ShipId.Invalid;
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
            var appliedDamage = Shield.CurrentValue <= 0 ? Health.ApplyDamage(damage) : Shield.ApplyDamage(damage);
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
            if (attackShip) LastAttackerId = attackShip.Id;
        }
        
        private void BroadcastDeath()
        {
            var victimId = myShip ? myShip.Id : ShipId.Invalid;
            OnDeath?.Invoke(victimId, LastAttackerId);
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
            LastAttackerId = ShipId.Invalid;
        }
        
        public void PopulateSettings(ResolvedShipStats s)
        {
            if (s == null) return;

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

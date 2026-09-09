using System;
using Damage;
using UnityEngine;
using Ships.Registry;

namespace Ships.Damage
{
    public class DamageController : MonoBehaviour, IDamageable, IDamageEvents
    {
        public event Action<DamageInfo> OnDamaged; // amount = applied (absorbed by shield + hull)
        public event Action<ShipId, DamageInfo> OnDeath; // victimId, killing blow

        public float maxHealth = 100f;
        public float maxShield = 100f;
        public float shieldRegenDelay = 3f;
        public float shieldRegenRate = 25f;

        public Resource Health { get; private set; }
        public RegenResource Shield { get; private set; }

        private Ship myShip;
        private bool deathFired;

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

        public void TakeDamage(in DamageInfo hit)
        {
            if (hit.Amount <= 0 || IsInvulnerable) return;
            var shieldAbsorbed = Shield.ApplyDamage(hit.Amount);
            var appliedDamage = shieldAbsorbed + Health.ApplyDamage(hit.Amount - shieldAbsorbed);
            var applied = hit.WithAmount(appliedDamage);
            if (appliedDamage > 0)
            {
                OnDamaged?.Invoke(applied);
            }

            if (Health.CurrentValue <= 0f)
            {
                BroadcastDeath(applied);
            }
        }

        private void BroadcastDeath(in DamageInfo killingBlow)
        {
            // One physics step can deliver several lethal hits; only the first is the killing blow.
            if (deathFired) return;
            deathFired = true;
            var victimId = myShip ? myShip.Id : ShipId.Invalid;
            OnDeath?.Invoke(victimId, killingBlow);
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
            deathFired = false;
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

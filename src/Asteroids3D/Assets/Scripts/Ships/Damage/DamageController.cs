using System;
using Damage;
using UnityEngine;
using Ships.Keystones;
using Ships.Loadout;
using Ships.Registry;

namespace Ships.Damage
{
    public class DamageController : MonoBehaviour, IDamageable, IDamageEvents
    {
        public event Action<AppliedDamage> OnDamaged;
        public event Action<ShipId, DamageInfo> OnDeath; // victimId, killing blow
        public event Action<DamageInfo> OnBlocked; // dropped by invulnerability or the keystone

        public float maxHealth = 100f;
        public float maxShield = 100f;
        public float shieldRegenDelay = 3f;
        public float shieldRegenRate = 25f;

        public Resource Health { get; private set; }
        public RegenResource Shield { get; private set; }

        private Ship myShip;
        private Keystone keystone;
        private bool deathFired;

        private float invulnerableUntil;
        public bool IsInvulnerable { get; private set; }
        
        public float InvulTimeLeft => invulnerableUntil - Time.time;

        private void Awake()
        {
            myShip = GetComponent<Ship>();
            Health = new Resource(maxHealth);
            Shield = new RegenResource(maxShield, shieldRegenRate, shieldRegenDelay);
            DamageableColliders.Register(this, GetComponentsInChildren<Collider>(true));
        }

        private void OnDestroy() => DamageableColliders.Unregister(this);

        private void Update()
        {
            IsInvulnerable = IsInvulnerable && InvulTimeLeft >= 0; 
            Shield.Update(Time.deltaTime);
        }

        public void Initialize(Keystone keystone) => this.keystone = keystone;

        public float TakeDamage(in DamageInfo hit)
        {
            if (hit.Amount <= 0) return 0f;
            if (IsInvulnerable || keystone && keystone.Blocks(in hit))
            {
                OnBlocked?.Invoke(hit);
                return 0f;
            }
            var shieldAbsorbed = hit.BypassesShield ? 0f : Shield.ApplyDamage(hit.Amount);
            var hullDamage = Health.ApplyDamage(hit.Amount - shieldAbsorbed);
            var applied = new AppliedDamage(hit, shieldAbsorbed, hullDamage);
            if (applied.Info.Amount > 0)
            {
                OnDamaged?.Invoke(applied);
            }

            if (Health.CurrentValue > 0f) return 0f;

            BroadcastDeath(applied.Info);
            return hit.Amount - applied.Info.Amount;
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

        /// <summary>A mid-run max change: the bars keep their fractions and the regen clock keeps running.</summary>
        public void Rescale(ResolvedShipStats s)
        {
            maxHealth       = s.maxHealth;
            maxShield       = s.maxShield;
            shieldRegenDelay= s.shieldRegenDelay;
            shieldRegenRate = s.shieldRegenRate;

            Health.Rescale(maxHealth);
            Shield.Rescale(maxShield, shieldRegenRate, shieldRegenDelay);
        }
    }
}

using Damage;
using Ships.Damage;
using Ships.Presentation;
using Ships.Registry;
using UnityEngine;
using Utils;

namespace Ships.Visuals
{
    public sealed class HullDamageEffects : MonoBehaviour, IShipVisual
    {
        [SerializeField] private ParticleSystem smoke;
        [SerializeField] private PooledVFX sparksPrefab;
        [SerializeField] private GameObject explosionPrefab;
        [SerializeField] private GameObject collisionExplosionPrefab;

        private IDamageEvents source;
        private bool subscribed;
        private PooledVFX pooledExplosion;
        private PooledVFX pooledCollisionExplosion;

        private void Awake()
        {
            if (explosionPrefab) pooledExplosion = explosionPrefab.GetComponent<PooledVFX>();
            if (collisionExplosionPrefab) pooledCollisionExplosion = collisionExplosionPrefab.GetComponent<PooledVFX>();
        }

        public void Bind(in ShipView view)
        {
            Unsubscribe();
            source = view.Damage;
            if (!isActiveAndEnabled) return;
            Subscribe();
            ApplyHealth();
        }

        private void OnEnable()
        {
            Subscribe();
            ApplyHealth();
        }

        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (subscribed || source == null) return;
            source.Health.OnValueChanged += OnHealthChanged;
            source.OnDamaged += SpawnSparks;
            source.OnDeath += OnDeath;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            source.Health.OnValueChanged -= OnHealthChanged;
            source.OnDamaged -= SpawnSparks;
            source.OnDeath -= OnDeath;
            subscribed = false;
        }

        private void OnHealthChanged(float current, float previous, float max) => ApplyHealth();

        private void ApplyHealth()
        {
            if (source == null) return;
            if (smoke) smoke.gameObject.SetActive(source.Health.Pct < 0.5f);
        }

        private void SpawnSparks(AppliedDamage damage)
        {
            if (sparksPrefab && damage.Info.Amount > 0f)
                SimplePool<PooledVFX>.Get(sparksPrefab, damage.Info.HitPoint, Quaternion.identity);
        }

        private void OnDeath(ShipId victim, DamageInfo hit)
        {
            var prefab = hit.Kind == DamageKind.Collision && collisionExplosionPrefab
                ? collisionExplosionPrefab : explosionPrefab;
            if (!prefab) return;
            var pooled = prefab == collisionExplosionPrefab ? pooledCollisionExplosion : pooledExplosion;
            if (pooled) SimplePool<PooledVFX>.Get(pooled, transform.position, Quaternion.identity);
            else Instantiate(prefab, transform.position, Quaternion.identity);
        }
    }
}

using Damage;
using Substrate.Presentation;
using UnityEngine;
using Utils;

namespace Combat.Projectiles.Visual
{
    [RequireComponent(typeof(ProjectileBase))]
    public class ProjectileHitVfx : MonoBehaviour, IPresentationPart
    {
        [SerializeField] private GameObject hitEffect;
        [SerializeField] private bool alignWithProjectile;

        private ProjectileBase projectile;
        private PooledVFX pooledHitEffect;

        private void Awake()
        {
            projectile = GetComponent<ProjectileBase>();
            pooledHitEffect = hitEffect ? hitEffect.GetComponent<PooledVFX>() : null;
        }

        private void OnEnable()
        {
            if (!projectile) return;
            projectile.Hit += HandleHit;
        }

        private void OnDisable()
        {
            if (!projectile) return;
            projectile.Hit -= HandleHit;
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void HandleHit(Vector3 position, IDamageable _)
        {
            if (!hitEffect) return;
            var rotation = alignWithProjectile ? transform.rotation : Quaternion.identity;
            if (pooledHitEffect) SimplePool<PooledVFX>.Get(pooledHitEffect, position, rotation);
            else Instantiate(hitEffect, position, rotation);
        }
    }
}

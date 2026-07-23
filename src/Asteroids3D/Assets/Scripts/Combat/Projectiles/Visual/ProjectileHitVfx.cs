using Damage;
using Game.Presentation;
using UnityEngine;
using Utils;

namespace Combat.Projectile.Visual
{
    [RequireComponent(typeof(ProjectileBase))]
    public class ProjectileHitVfx : MonoBehaviour, IPresentationPart
    {
        [SerializeField] private GameObject hitEffect;

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
            if (!hitEffect || !GameSettings.VfxEnabled) return;
            if (pooledHitEffect) SimplePool<PooledVFX>.Get(pooledHitEffect, position, Quaternion.identity);
            else Instantiate(hitEffect, position, Quaternion.identity);
        }
    }
}

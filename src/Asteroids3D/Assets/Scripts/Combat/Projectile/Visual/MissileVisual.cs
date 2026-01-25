using Game;
using UnityEngine;
using Utils;

namespace Combat.Projectile.Visual
{
    [RequireComponent(typeof(Missile))]
    public sealed class MissileVisual : MonoBehaviour
    {
        [SerializeField] private PooledVFX explosionPrefab;

        private Missile missile;

        private void Awake()
        {
            missile = GetComponent<Missile>();
        }

        private void OnEnable()
        {
            missile.OnDetonated += HandleDetonated;
        }

        private void OnDisable()
        {
            missile.OnDetonated -= HandleDetonated;
        }

        private void HandleDetonated(Vector3 position)
        {
            if (!GameSettings.VfxEnabled || !explosionPrefab) return;
            SimplePool<PooledVFX>.Get(explosionPrefab, position, Quaternion.identity);
        }
    }
}

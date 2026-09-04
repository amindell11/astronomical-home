using Game.Presentation;
using UnityEngine;
using Utils;

namespace Combat.Projectiles.Visual
{
    [RequireComponent(typeof(Missile))]
    public sealed class MissileVisual : MonoBehaviour, IPresentationPart
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

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void HandleDetonated(Vector3 position)
        {
            if (!explosionPrefab) return;
            SimplePool<PooledVFX>.Get(explosionPrefab, position, Quaternion.identity);
        }
    }
}

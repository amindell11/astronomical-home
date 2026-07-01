using UnityEngine;
using Utils;

namespace Asteroids.Visual
{
    [RequireComponent(typeof(AsteroidController))]
    public sealed class AsteroidVisual : MonoBehaviour
    {
        [SerializeField] private GameObject explosionPrefab;

        private AsteroidController asteroid;
        private Renderer cachedRenderer;
        private PooledVFX pooledExplosion;

        private void Awake()
        {
            asteroid = GetComponent<AsteroidController>();
            cachedRenderer = GetComponent<Renderer>();
            pooledExplosion = explosionPrefab ? explosionPrefab.GetComponent<PooledVFX>() : null;
        }

        private void OnEnable()
        {
            if (!asteroid) return;
            asteroid.OnDestroyed += HandleDestroyed;
            if (cachedRenderer) cachedRenderer.enabled = true;
        }

        private void OnDisable()
        {
            if (!asteroid) return;
            asteroid.OnDestroyed -= HandleDestroyed;
        }

        private void HandleDestroyed(Vector3 position)
        {
            if (cachedRenderer) cachedRenderer.enabled = false;
            if (!explosionPrefab) return;
            if (pooledExplosion)
            {
                SimplePool<PooledVFX>.Get(pooledExplosion, position, Quaternion.identity);
                return;
            }

            Instantiate(explosionPrefab, position, Quaternion.identity);
        }
    }
}

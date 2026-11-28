using Substrate.Presentation;
using UnityEngine;
using Utils;

namespace Asteroids.Visual
{
    [RequireComponent(typeof(AsteroidController))]
    public sealed class AsteroidVisual : MonoBehaviour, IPresentationPart
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private GameObject explosionPrefab;
        [Tooltip("Surface colour of a fragment thrown by Shrapnel rocks, while it hits harder.")]
        [SerializeField] private Color thrownTint = new(2.4f, 0.9f, 0.25f, 1f);

        private AsteroidController asteroid;
        private AsteroidDamage damage;
        private Renderer cachedRenderer;
        private PooledVFX pooledExplosion;
        private MaterialPropertyBlock thrownBlock;

        private void Awake()
        {
            asteroid = GetComponent<AsteroidController>();
            damage = GetComponent<AsteroidDamage>();
            cachedRenderer = GetComponent<Renderer>();
            pooledExplosion = explosionPrefab ? explosionPrefab.GetComponent<PooledVFX>() : null;
            thrownBlock = new MaterialPropertyBlock();
            thrownBlock.SetColor(BaseColorId, thrownTint);
        }

        private void OnEnable()
        {
            if (!asteroid) return;
            asteroid.OnDestroyed += HandleDestroyed;
            asteroid.OnInitialized += Cool;
            damage.Thrown += Heat;
            if (cachedRenderer) cachedRenderer.enabled = true;
        }

        private void OnDisable()
        {
            if (!asteroid) return;
            asteroid.OnDestroyed -= HandleDestroyed;
            asteroid.OnInitialized -= Cool;
            damage.Thrown -= Heat;
            Cool();
        }

        private void Heat()
        {
            if (cachedRenderer) cachedRenderer.SetPropertyBlock(thrownBlock);
        }

        private void Cool()
        {
            if (cachedRenderer) cachedRenderer.SetPropertyBlock(null);
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

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

using UnityEngine;
using UnityEngine.Pool;

namespace Asteroids.Spawning
{
    public class SpawnPool
    {
        private readonly AsteroidController prefab;
        private readonly ObjectPool<AsteroidController> pool;
        private readonly Transform parent;
        
        public SpawnPool(AsteroidSpawnSettings settings, Transform parentTransform, int maxSizeHint = 0){
            prefab = settings.asteroidPrefab;
            parent = parentTransform;
            pool = new ObjectPool<AsteroidController>(
                CreatePooledAsteroid,
                OnAsteroidRetrieved,
                OnAsteroidReleased,
                OnAsteroidDestroyed,
                false,
                settings.poolCapacity,
                Mathf.Max(settings.maxPoolSize, maxSizeHint)
            );
            PreWarm(settings.poolCapacity);
        }

        private void PreWarm(int count)
        {
            for (var i = 0; i < count; ++i) {
                var obj = pool.Get();
                pool.Release(obj);
            }
        }
        public void ReleaseAsteroid(AsteroidController ast) => pool.Release(ast);
        private static void OnAsteroidRetrieved(AsteroidController ast) => ast.gameObject.SetActive(true);
        private static void OnAsteroidReleased(AsteroidController ast) => ast.gameObject.SetActive(false);
        private static void OnAsteroidDestroyed(AsteroidController ast)=> Object.Destroy(ast);
        private AsteroidController CreatePooledAsteroid() => Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, parent);
        public AsteroidController Get() => pool.Get();
    }
}

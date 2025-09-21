using UnityEngine;
using UnityEngine.Pool;

namespace Asteroids.Spawning
{
    public class SpawnPool
    {
        private readonly Asteroid prefab;
        private readonly ObjectPool<Asteroid> pool;
        private readonly Transform parent;
        
        public SpawnPool(SpawnSettings settings, Transform parentTransform){
            prefab = settings.asteroidPrefab;
            parent = parentTransform;
            pool = new ObjectPool<Asteroid>(
                CreatePooledAsteroid,
                OnAsteroidRetrieved,
                OnAsteroidReleased,
                OnAsteroidDestroyed,
                false,
                settings.poolCapacity,
                settings.maxPoolSize
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
        public void ReleaseAsteroid(Asteroid ast) => pool.Release(ast);
        private static void OnAsteroidRetrieved(Asteroid ast) => ast.gameObject.SetActive(true);
        private static void OnAsteroidReleased(Asteroid ast) => ast.gameObject.SetActive(false);
        private static void OnAsteroidDestroyed(Asteroid ast)=> Object.Destroy(ast);
        private Asteroid CreatePooledAsteroid() => Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, parent);
        public Asteroid Get() => pool.Get();
    }
}
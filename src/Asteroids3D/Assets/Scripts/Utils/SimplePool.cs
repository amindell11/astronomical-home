using System.Collections.Generic;
using UnityEngine;

namespace Utils
{
    /// <summary>
    /// A simple generic object pool for MonoBehaviour components.
    /// Automatically handles creation, retrieval, and release of pooled objects.
    /// </summary>
    public static class SimplePool<T> where T : MonoBehaviour
    {
        // Pools are now tracked per prefab instance ID to avoid mixing different prefabs that
        // share the same component type (e.g., sparks vs explosions both using PooledVFX).
        private static readonly Dictionary<int, Stack<T>> Pools = new(); // prefabID -> instances
        private static readonly Dictionary<T, int> InstanceToKey = new(); // instance -> prefabID
        private static Transform _poolParent;
    
        /// <summary>
        /// Get an object from the pool, or create a new one if none available
        /// </summary>
        public static T Get(T prefab, Vector3 position, Quaternion rotation)
        {
            var key = prefab.GetInstanceID();
            var stack = GetOrCreateStack(key);

            // Pooled instances can be destroyed out from under the static pool (play sessions with
            // domain reload disabled, scene transitions, test teardown). Skip corpses instead of
            // handing them out.
            while (stack.Count > 0)
            {
                var pooled = stack.Pop();
                if (!pooled)
                {
                    InstanceToKey.Remove(pooled);
                    continue;
                }

                pooled.transform.position = position;
                pooled.transform.rotation = rotation;
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            var instance = Object.Instantiate(prefab, position, rotation);

            // Set up pool parent for organization
            EnsurePoolParent();

            instance.transform.SetParent(_poolParent);
            instance.gameObject.SetActive(true);
            // Track which prefab pool this instance belongs to
            InstanceToKey[instance] = key;

            return instance;
        }
    
        /// <summary>
        /// Return an object to the pool
        /// </summary>
        public static void Release(T instance)
        {
            if (!instance) return;
        
            instance.gameObject.SetActive(false);

            if (!InstanceToKey.TryGetValue(instance, out var key))
            {
                // Fallback: if mapping missing, push into a generic pool keyed by 0
                key = 0;
            }

            var stack = GetOrCreateStack(key);

            stack.Push(instance);
        }

        public static void Warm(T prefab, int preloadCount = 1)
        {
            if (!prefab) return;

            var key = prefab.GetInstanceID();
            var stack = GetOrCreateStack(key);
            EnsurePoolParent();

            while (stack.Count < preloadCount)
            {
                var instance = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
                instance.transform.SetParent(_poolParent);
                instance.gameObject.SetActive(false);
                InstanceToKey[instance] = key;
                stack.Push(instance);
            }
        }
    
        /// <summary>
        /// Clear the entire pool (useful for scene transitions)
        /// </summary>
        public static void Clear()
        {
            foreach (var stack in Pools.Values)
            {
                while (stack.Count > 0)
                {
                    var instance = stack.Pop();
                    if (instance != null)
                        Object.Destroy(instance.gameObject);
                }
            }
            Pools.Clear();
            InstanceToKey.Clear();
        }

        private static Stack<T> GetOrCreateStack(int key)
        {
            if (Pools.TryGetValue(key, out var stack))
                return stack;

            stack = new Stack<T>();
            Pools[key] = stack;
            return stack;
        }

        private static void EnsurePoolParent()
        {
            if (_poolParent) return;

            var poolObj = new GameObject($"Pool_{typeof(T).Name}");
            _poolParent = poolObj.transform;
            Object.DontDestroyOnLoad(poolObj);
        }
    
        /// <summary>
        /// Get current pool size for debugging
        /// </summary>
        public static int PoolSize
        {
            get
            {
                var total = 0;
                foreach (var stack in Pools.Values)
                    total += stack.Count;
                return total;
            }
        }
    }

    /// <summary>
    /// Global pool management utilities
    /// </summary>
    public static class SimplePoolManager
    {
        /// <summary>
        /// Clear all pools of all types - useful for scene transitions
        /// </summary>
        public static void ClearAllPools()
        {
            // Use reflection to find all generic pool types and clear them
            var poolTypes = new System.Type[]
            {
                typeof(SimplePool<>).MakeGenericType(typeof(global::Audio.PooledAudioSource)),
                // Add other commonly used pooled types here as needed
            };

            foreach (var poolType in poolTypes)
            {
                var clearMethod = poolType.GetMethod("Clear", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                clearMethod?.Invoke(null, null);
            }
        }
    }
}

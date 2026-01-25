using System.Collections.Generic;
using System.Linq;
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

            if (!Pools.TryGetValue(key, out var stack))
            {
                stack = new Stack<T>();
                Pools[key] = stack;
            }

            T instance;

            if (stack.Count > 0)
            {
                instance = stack.Pop();
                instance.transform.position = position;
                instance.transform.rotation = rotation;
                instance.gameObject.SetActive(true);
            }
            else
            {
                instance = Object.Instantiate(prefab, position, rotation);

                // Set up pool parent for organization
                if (!_poolParent)
                {
                    var poolObj = new GameObject($"Pool_{typeof(T).Name}");
                    _poolParent = poolObj.transform;
                    Object.DontDestroyOnLoad(poolObj);
                }

                instance.transform.SetParent(_poolParent);
                instance.gameObject.SetActive(true);
                // Track which prefab pool this instance belongs to
                InstanceToKey[instance] = key;
            }

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

            if (!Pools.TryGetValue(key, out var stack))
            {
                stack = new Stack<T>();
                Pools[key] = stack;
            }

            stack.Push(instance);
        }
    
        /// <summary>
        /// Clear the entire pool (useful for scene transitions)
        /// </summary>
        public static void Clear()
        {
            foreach (var stack in Pools.Select(kvp => kvp.Value))
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

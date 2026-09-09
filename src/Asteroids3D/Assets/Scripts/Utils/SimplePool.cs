using System.Collections.Generic;
using UnityEngine;

namespace Utils
{
    /// <summary>Simple static object pool for MonoBehaviour components.</summary>
    public static class SimplePool<T> where T : MonoBehaviour
    {
        // Keyed per prefab instance ID so different prefabs sharing a component type (sparks vs explosions, both PooledVFX) never mix.
        private static readonly Dictionary<int, Stack<T>> Pools = new();
        private static readonly Dictionary<T, int> InstanceToKey = new();
        private static Transform _poolParent;

        public static T Get(T prefab, Vector3 position, Quaternion rotation)
        {
            var key = prefab.GetInstanceID();
            var stack = GetOrCreateStack(key);

            // Pooled instances can be destroyed out from under the static pool (domain reload disabled, scene transitions, test teardown) — skip corpses instead of handing them out.
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

            EnsurePoolParent();

            instance.transform.SetParent(_poolParent);
            instance.gameObject.SetActive(true);
            InstanceToKey[instance] = key;

            return instance;
        }

        public static void Release(T instance)
        {
            if (!instance) return;

            // Inactive-self ⇔ already pooled: a duplicate push would deal the same instance out to two callers.
            if (!instance.gameObject.activeSelf)
            {
                Debug.LogError($"SimplePool<{typeof(T).Name}>: {instance.name} released while already inactive — double release; refusing duplicate pool push.", instance);
                return;
            }

            instance.gameObject.SetActive(false);

            if (!InstanceToKey.TryGetValue(instance, out var key))
            {
                // Unmapped instance (never Get/Warm'd): park it in a shared pool rather than lose it.
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
}

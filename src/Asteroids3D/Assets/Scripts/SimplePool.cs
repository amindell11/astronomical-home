using UnityEngine;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// A simple generic object pool for MonoBehaviour components.
/// Automatically handles creation, retrieval, and release of pooled objects.
/// </summary>
public static class SimplePool<T> where T : MonoBehaviour
{
    // Pools are now tracked per prefab instance ID to avoid mixing different prefabs that
    // share the same component type (e.g., sparks vs explosions both using PooledVFX).
    private static readonly Dictionary<int, Stack<T>> pools = new(); // prefabID -> instances
    private static readonly Dictionary<T, int> instanceToKey = new(); // instance -> prefabID
    private static Transform poolParent;
    
    /// <summary>
    /// Get an object from the pool, or create a new one if none available
    /// </summary>
    public static T Get(T prefab, Vector3 position, Quaternion rotation)
    {
        int key = prefab.GetInstanceID();

        if (!pools.TryGetValue(key, out var stack))
        {
            stack = new Stack<T>();
            pools[key] = stack;
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
            if (poolParent == null)
            {
                var poolGO = new GameObject($"Pool_{typeof(T).Name}");
                poolParent = poolGO.transform;
                Object.DontDestroyOnLoad(poolGO);
            }

            instance.transform.SetParent(poolParent);
            // Track which prefab pool this instance belongs to
            instanceToKey[instance] = key;
        }

        return instance;
    }
    
    /// <summary>
    /// Return an object to the pool
    /// </summary>
    public static void Release(T instance)
    {
        if (instance == null) return;
        
        instance.gameObject.SetActive(false);

        if (!instanceToKey.TryGetValue(instance, out int key))
        {
            // Fallback: if mapping missing, push into a generic pool keyed by 0
            key = 0;
        }

        if (!pools.TryGetValue(key, out var stack))
        {
            stack = new Stack<T>();
            pools[key] = stack;
        }

        stack.Push(instance);
    }
    
    /// <summary>
    /// Clear the entire pool (useful for scene transitions)
    /// </summary>
    public static void Clear()
    {
        foreach (var kvp in pools)
        {
            var stack = kvp.Value;
            while (stack.Count > 0)
            {
                var instance = stack.Pop();
                if (instance != null)
                    Object.Destroy(instance.gameObject);
            }
        }
        pools.Clear();
        instanceToKey.Clear();
    }
    
    /// <summary>
    /// Get current pool size for debugging
    /// </summary>
    public static int PoolSize
    {
        get
        {
            int total = 0;
            foreach (var stack in pools.Values)
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
            typeof(SimplePool<>).MakeGenericType(typeof(PooledAudioSource)),
            // Add other commonly used pooled types here as needed
        };

        foreach (var poolType in poolTypes)
        {
            var clearMethod = poolType.GetMethod("Clear", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            clearMethod?.Invoke(null, null);
        }
    }
} 
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A simple generic object pool for MonoBehaviour components.
/// Automatically handles creation, retrieval, and release of pooled objects.
/// </summary>
public static class SimplePool<T> where T : MonoBehaviour
{
    private static readonly Stack<T> pool = new Stack<T>();
    private static Transform poolParent;
    
    /// <summary>
    /// Get an object from the pool, or create a new one if none available
    /// </summary>
    public static T Get(T prefab, Vector3 position, Quaternion rotation)
    {
        T instance;
        
        if (pool.Count > 0)
        {
            instance = pool.Pop();
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
        pool.Push(instance);
    }
    
    /// <summary>
    /// Clear the entire pool (useful for scene transitions)
    /// </summary>
    public static void Clear()
    {
        while (pool.Count > 0)
        {
            var instance = pool.Pop();
            if (instance != null)
                Object.Destroy(instance.gameObject);
        }
    }
    
    /// <summary>
    /// Get current pool size for debugging
    /// </summary>
    public static int PoolSize => pool.Count;
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
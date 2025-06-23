using UnityEngine;

[CreateAssetMenu(fileName = "New Asteroid Spawn Settings", menuName = "Asteroid/Spawn Settings")]
public class AsteroidSpawnSettings : ScriptableObject
{
    [Header("Mesh Assets")]
    [Tooltip("Array of asteroid meshes to randomly select from when spawning")]
    public Mesh[] asteroidMeshes;

    [Header("Randomization Ranges")]
    [Tooltip("The range of random mass scaling applied to newly spawned asteroids")]
    public Vector2 massScaleRange = new Vector2(0.5f, 2f);
    
    [Tooltip("The base velocity range, which gets scaled by mass")]
    public Vector2 velocityRange = new Vector2(0.5f, 2f);
    
    [Tooltip("The base spin range, which gets scaled by mass")]
    public Vector2 spinRange = new Vector2(-30f, 30f);
    
    [Header("Pool Settings")]
    [Tooltip("Initial capacity of the asteroid object pool")]
    public int defaultPoolCapacity = 20;
    
    [Tooltip("Maximum size the asteroid object pool can grow to")]
    public int maxPoolSize = 100;

    [Header("Physical Properties")]
    [Tooltip("Default density for asteroids (used for mass calculations)")]
    public float defaultDensity = 1000f;

    /// <summary>
    /// Get a random mesh from the available asteroid meshes
    /// </summary>
    /// <returns>A random asteroid mesh, or null if no meshes are available</returns>
    public Mesh GetRandomMesh()
    {
        if (asteroidMeshes == null || asteroidMeshes.Length == 0)
        {
            return null;
        }
        return asteroidMeshes[Random.Range(0, asteroidMeshes.Length)];
    }

    /// <summary>
    /// Calculate velocity scale factor based on mass
    /// </summary>
    /// <param name="mass">The mass of the asteroid</param>
    /// <returns>Velocity scale factor (smaller for heavier asteroids)</returns>
    public float GetVelocityScale(float mass)
    {
        return (mass > 0) ? 1f / Mathf.Pow(mass, 1f/3f) : 1f;
    }

    /// <summary>
    /// Generate random velocity within the configured range, scaled by mass
    /// </summary>
    /// <param name="mass">The mass of the asteroid</param>
    /// <returns>Random velocity vector</returns>
    public Vector3 GetRandomVelocity(float mass)
    {
        float velocityScale = GetVelocityScale(mass);
        return Random.insideUnitCircle.normalized * 
               Random.Range(velocityRange.x, velocityRange.y) * velocityScale;
    }

    /// <summary>
    /// Generate random angular velocity within the configured range, scaled by mass
    /// </summary>
    /// <param name="mass">The mass of the asteroid</param>
    /// <returns>Random angular velocity vector</returns>
    public Vector3 GetRandomAngularVelocity(float mass)
    {
        float velocityScale = GetVelocityScale(mass);
        return new Vector3(
            Random.Range(spinRange.x, spinRange.y) * velocityScale,
            Random.Range(spinRange.x, spinRange.y) * velocityScale,
            Random.Range(spinRange.x, spinRange.y) * velocityScale
        );
    }

    /// <summary>
    /// Validate the settings and log warnings for any issues
    /// </summary>
    public void ValidateSettings()
    {
        if (asteroidMeshes == null || asteroidMeshes.Length == 0)
        {
            Debug.LogWarning($"AsteroidSpawnSettings '{name}': No asteroid meshes assigned!");
        }

        if (massScaleRange.x <= 0 || massScaleRange.y <= 0)
        {
            Debug.LogWarning($"AsteroidSpawnSettings '{name}': Mass scale range contains non-positive values!");
        }

        if (defaultPoolCapacity <= 0)
        {
            Debug.LogWarning($"AsteroidSpawnSettings '{name}': Default pool capacity should be greater than 0!");
        }

        if (maxPoolSize < defaultPoolCapacity)
        {
            Debug.LogWarning($"AsteroidSpawnSettings '{name}': Max pool size should be >= default pool capacity!");
        }

        if (defaultDensity <= 0)
        {
            Debug.LogWarning($"AsteroidSpawnSettings '{name}': Default density should be greater than 0!");
        }
    }

    private void OnValidate()
    {
        ValidateSettings();
    }
} 
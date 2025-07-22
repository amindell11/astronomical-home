using UnityEngine;
using UnityEngine.Pool;
using System.Collections.Generic;

public class AsteroidSpawner : MonoBehaviour
{
    public static AsteroidSpawner Instance { get; private set; }

    [Header("Asteroid Configuration")]
    [SerializeField] private GameObject asteroidPrefab;
    [SerializeField] private AsteroidSpawnSettings spawnSettings;

    private ObjectPool<GameObject> asteroidPool;

    // Book-keeping now lives in AsteroidRegistry.  These pass-through properties keep
    // existing callers/tests working without modification.
    public int ActiveAsteroidCount => AsteroidRegistry.Instance != null ? AsteroidRegistry.Instance.ActiveCount : 0;
    public float TotalActiveVolume => AsteroidRegistry.Instance != null ? AsteroidRegistry.Instance.TotalVolume : 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Only one spawner should exist after refactor – destroy duplicates to avoid ambiguity.
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Ensure a registry exists early so book-keeping works from the first spawn.
        if (AsteroidRegistry.Instance == null)
        {
            gameObject.AddComponent<AsteroidRegistry>();
        }

        // Ensure spawnSettings is assigned; this ScriptableObject now drives all spawn parameters.
        if (spawnSettings == null)
        {
            RLog.AsteroidError("AsteroidSpawner requires a reference to AsteroidSpawnSettings.");
            enabled = false;
            return;
        }

        // Validate settings early so any issues surface before runtime spawning.
        spawnSettings.ValidateSettings();

        // Initialize the asteroid object pool
        int poolCapacity = spawnSettings.defaultPoolCapacity;
        int poolMaxSize = spawnSettings.maxPoolSize;
        asteroidPool = new ObjectPool<GameObject>(
            CreatePooledAsteroid,
            OnAsteroidRetrieved,
            OnAsteroidReleased,
            OnAsteroidDestroyed,
            collectionCheck: false,
            defaultCapacity: poolCapacity,
            maxSize: poolMaxSize
        );

        // -------- Pre-warm the pool --------
        for (int i = 0; i < poolCapacity; ++i)
        {
            var obj = asteroidPool.Get();
            asteroidPool.Release(obj);
        }
    }

    public GameObject SpawnAsteroid(AsteroidSpawnRequest request)
    {
        GameObject go = asteroidPool.Get();
        go.transform.SetParent(transform);
        go.transform.SetPositionAndRotation(request.Pose.position, request.Pose.rotation);

        Asteroid a = go.GetComponent<Asteroid>();
        if (a == null)
        {
            RLog.AsteroidError("Pooled object is missing Asteroid component.");
            asteroidPool.Release(go);
            return null;
        }

        // Branch **once** on the high-level reason,
        // delegate the messy calculations to helpers.
        switch (request.Kind)
        {
            case AsteroidSpawnRequest.SpawnKind.Random:
                InitRandomAsteroid(a);
                break;

            case AsteroidSpawnRequest.SpawnKind.Fragment:
                InitFragmentAsteroid(
                    a,
                    request.Mass!.Value,
                    request.Velocity!.Value,
                    request.AngularVelocity!.Value);
                break;
        }

        // Register with central registry (handles volume + active count).
        AsteroidRegistry.Instance?.Register(a);
        return go;
    }

    public void ReleaseAsteroid(GameObject asteroidGO)
    {
        if (asteroidGO == null) 
        {
            return;
        }
        Asteroid asteroid = asteroidGO != null ? asteroidGO.GetComponent<Asteroid>() : null;
        if (asteroid != null)
        {
            AsteroidRegistry.Instance?.Unregister(asteroid);
        }
        asteroidPool.Release(asteroidGO);
    }

    public void ReleaseAllAsteroids()
    {
        if (AsteroidRegistry.Instance == null) return;

        // Snapshot to avoid modifying the collection while iterating.
        var toRelease = new List<Asteroid>(AsteroidRegistry.Instance.ActiveAsteroids);
        foreach (var ast in toRelease)
        {
            if (ast != null)
            {
                ReleaseAsteroid(ast.gameObject);
            }
        }
    }

    // ----------------- Helper initialisers -----------------
    private void InitRandomAsteroid(Asteroid asteroid)
    {
        // Determine mesh and mass/scale entirely from settings
        AsteroidSpawnSettings.MeshInfo meshInfo = spawnSettings.GetRandomMeshInfo();
        var (mass, scale) = CalculateMassAndScale(asteroid, meshInfo, null);

        Vector3 velocity = spawnSettings.GetRandomVelocity(mass);
        Vector3 angularVelocity = spawnSettings.GetRandomAngularVelocity(mass);

        asteroid.Initialize(meshInfo, mass, scale, velocity, angularVelocity);
    }

    private void InitFragmentAsteroid(
        Asteroid asteroid,
        float mass,
        Vector3 velocity,
        Vector3 angularVelocity)
    {
        // Use a random mesh but honour the supplied mass / kinematics
        AsteroidSpawnSettings.MeshInfo meshInfo = spawnSettings.GetRandomMeshInfo();
        var (finalMass, scale) = CalculateMassAndScale(asteroid, meshInfo, mass);

        asteroid.Initialize(meshInfo, finalMass, scale, velocity, angularVelocity);
    }

    // ----------------- Book-keeping -----------------
    // Tracking now handled by AsteroidRegistry – no local implementation needed.

    // --------- ObjectPool Callbacks ---------
    private GameObject CreatePooledAsteroid()
    {
        return Instantiate(asteroidPrefab, Vector3.zero, Quaternion.identity, transform.parent);
    }

    private void OnAsteroidRetrieved(GameObject asteroidGO)
    {
        asteroidGO.SetActive(true);
    }

    private void OnAsteroidReleased(GameObject asteroidGO)
    {
        asteroidGO.SetActive(false);
    }

    private void OnAsteroidDestroyed(GameObject asteroidGO)
    {
        Destroy(asteroidGO);
    }
    
    // Calculate mass and scale using the asteroid's density property
    private (float finalMass, float finalScale) CalculateMassAndScale(Asteroid asteroid, AsteroidSpawnSettings.MeshInfo meshInfo, float? mass)
    {
        float baseVolume = meshInfo.cachedVolume > 0f ? meshInfo.cachedVolume : (meshInfo.mesh != null ? meshInfo.mesh.bounds.size.x * meshInfo.mesh.bounds.size.y * meshInfo.mesh.bounds.size.z : 1f);
        float density = asteroid.Density;
        float baseMass = baseVolume * density;

        if (mass.HasValue)
        {
            float massScaleFactor = mass.Value / baseMass;
            float finalScale = Mathf.Pow(massScaleFactor, 1f / 3f);
            return (mass.Value, finalScale);
        }

        // Random mass based on range – treat as scaling factor for mass, not scale.
        Vector2 currentMassScaleRange = spawnSettings.massScaleRange;
        float randomScaleFactor = Random.Range(currentMassScaleRange.x, currentMassScaleRange.y);
        float finalScaleComputed = Mathf.Pow(randomScaleFactor, 1f / 3f);
        float finalMassComputed = baseMass * randomScaleFactor;
        return (finalMassComputed, finalScaleComputed);
    }
    
} 
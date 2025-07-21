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
    private readonly HashSet<GameObject> activeAsteroids = new HashSet<GameObject>();
    public int ActiveAsteroidCount => activeAsteroids.Count;
    public float TotalActiveVolume { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            // First spawner becomes the global fallback for legacy code. Others can coexist.
            Instance = this;
        }

        TotalActiveVolume = 0f;

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

        TrackActive(go, a.CurrentVolume);
        return go;
    }

    public void ReleaseAsteroid(GameObject asteroidGO)
    {
        if (asteroidGO == null) 
        {
            return;
        }
        // Attempt to remove from the active set first; only adjust counters if successful.
        bool wasInActiveSet = activeAsteroids.Remove(asteroidGO);

        Asteroid asteroid = asteroidGO.GetComponent<Asteroid>();
        float asteroidVolume = asteroid != null ? asteroid.CurrentVolume : 0f;

        if (wasInActiveSet && asteroid != null)
        {
            float previousVolume = TotalActiveVolume;
            TotalActiveVolume -= asteroidVolume;
            RLog.Asteroid($"AsteroidSpawner: RELEASED asteroid {asteroidGO.name} | Volume: {asteroidVolume:F2} | Total Volume: {previousVolume:F2} -> {TotalActiveVolume:F2}");
        }

        asteroidPool.Release(asteroidGO);
    }

    public void ReleaseAllAsteroids()
    {
        // Remove asteroids one by one without allocating a snapshot list.
        while (activeAsteroids.Count > 0)
        {
            var enumerator = activeAsteroids.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                break; // Safety – should not happen.
            }
            ReleaseAsteroid(enumerator.Current);
            // ReleaseAsteroid will remove it from activeAsteroids.
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
    private void TrackActive(GameObject go, float asteroidVolume)
    {
        activeAsteroids.Add(go);
        float previousVolume = TotalActiveVolume;
        TotalActiveVolume += asteroidVolume;
        RLog.Asteroid($"AsteroidSpawner: SPAWNED asteroid {go.name} | Volume: {asteroidVolume:F2} | Total Volume: {previousVolume:F2} -> {TotalActiveVolume:F2} | Active Count: {activeAsteroids.Count}");
    }

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
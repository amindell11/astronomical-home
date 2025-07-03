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
    }

    public GameObject SpawnAsteroid(Pose pose, float? mass = null, Vector3? velocity = null, Vector3? angularVelocity = null)
    {
        // Retrieve (or create) an asteroid from the pool
        GameObject asteroidGO = asteroidPool.Get();
        asteroidGO.transform.SetParent(transform); // ensure correct hierarchy
        asteroidGO.transform.SetPositionAndRotation(pose.position, pose.rotation);

        Asteroid asteroid = asteroidGO.GetComponent<Asteroid>();
        if (asteroid == null)
        {
            RLog.AsteroidError("Pooled object is missing Asteroid component.");
            asteroidPool.Release(asteroidGO);
            return null;
        }

        // --- Determine final properties before initialization ---
        Mesh randomMesh = GetRandomMesh();
        var (finalMass, finalScale) = CalculateMassAndScale(asteroid, randomMesh, mass);

        // Calculate velocity and spin using spawn settings if available
        Vector3 finalVelocity = velocity ?? spawnSettings.GetRandomVelocity(finalMass);

        Vector3 finalAngularVelocity = angularVelocity ?? spawnSettings.GetRandomAngularVelocity(finalMass);

        // Initialize the asteroid with the calculated properties
        asteroid.Initialize(
            randomMesh,
            finalMass,
            finalScale,
            finalVelocity,
            finalAngularVelocity
        );
        activeAsteroids.Add(asteroidGO);
        TotalActiveVolume += asteroid.CurrentVolume;
        return asteroidGO;
    }

    public void ReleaseAsteroid(GameObject asteroidGO)
    {
        if (asteroidGO == null) return;

        Asteroid asteroid = asteroidGO.GetComponent<Asteroid>();
        if (asteroid != null)
        {
            TotalActiveVolume -= asteroid.CurrentVolume;
        }

        activeAsteroids.Remove(asteroidGO);
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
        Rigidbody rb = asteroidGO.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void OnAsteroidDestroyed(GameObject asteroidGO)
    {
        Destroy(asteroidGO);
    }
    
    // Calculate mass and scale using the asteroid's density property
    private (float finalMass, float finalScale) CalculateMassAndScale(Asteroid asteroid, Mesh mesh, float? mass)
    {
        if (mass.HasValue)
        {
            // We have a target mass, calculate the scale needed to achieve it
            float baseMass = Asteroid.CalculateMassFromMesh(mesh, asteroid.Density, 1f);
            float massScaleFactor = mass.Value / baseMass;
            float finalScale = Mathf.Pow(massScaleFactor, 1f/3f);
            return (mass.Value, finalScale);
        }
        else
        {
            // Random scale, calculate mass from that scale
            Vector2 currentMassScaleRange = spawnSettings.massScaleRange;
            float randomScaleFactor = Random.Range(currentMassScaleRange.x, currentMassScaleRange.y);
            float finalScale = Mathf.Pow(randomScaleFactor, 1f/3f);
            float finalMass = Asteroid.CalculateMassFromMesh(mesh, asteroid.Density, finalScale);
            return (finalMass, finalScale);
        }
    }

    private Mesh GetRandomMesh()
    {
        return spawnSettings.GetRandomMesh();
    }
} 
using UnityEngine;
using UnityEngine.Pool;

public class AsteroidSpawner : MonoBehaviour
{
    public static AsteroidSpawner Instance { get; private set; }

    [Header("Asteroid Prefab")]
    [SerializeField] private GameObject asteroidPrefab;

    [Header("Mesh Assets")]
    [SerializeField] private Mesh[] asteroidMeshes;

    [Header("Randomization Ranges")]
    [Tooltip("The range of random mass scaling applied to newly spawned asteroids.")]
    [SerializeField] public Vector2 massScaleRange = new Vector2(0.5f, 2f);
    [Tooltip("The base velocity range, which gets scaled by mass.")]
    [SerializeField] public Vector2 velocityRange = new Vector2(0.5f, 2f);
    [Tooltip("The base spin range, which gets scaled by mass.")]
    [SerializeField] public Vector2 spinRange = new Vector2(-30f, 30f);
    
    [Header("Asteroid Pool Settings")]
    [SerializeField] private int defaultPoolCapacity = 20;
    [SerializeField] private int maxPoolSize = 100;

    private ObjectPool<GameObject> asteroidPool;
    public int ActiveAsteroidCount => asteroidPool.CountActive;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        // Initialize the asteroid object pool
        asteroidPool = new ObjectPool<GameObject>(
            CreatePooledAsteroid,
            OnAsteroidRetrieved,
            OnAsteroidReleased,
            OnAsteroidDestroyed,
            collectionCheck: false,
            defaultCapacity: defaultPoolCapacity,
            maxSize: maxPoolSize
        );
    }

    public GameObject SpawnAsteroid(Pose pose, float? mass = null, Vector3? velocity = null, Vector3? angularVelocity = null)
    {
        // Retrieve (or create) an asteroid from the pool
        GameObject asteroidGO = asteroidPool.Get();
        asteroidGO.transform.SetParent(transform.parent); // ensure correct hierarchy
        asteroidGO.transform.SetPositionAndRotation(pose.position, pose.rotation);

        Asteroid asteroid = asteroidGO.GetComponent<Asteroid>();
        if (asteroid == null)
        {
            RLog.LogError("Pooled object is missing Asteroid component.");
            asteroidPool.Release(asteroidGO);
            return null;
        }

        // --- Determine final properties before initialization ---
        Mesh randomMesh = GetRandomMesh();
        var (finalMass, finalScale) = CalculateMassAndScale(asteroid, randomMesh, mass);

        // Calculate velocity and spin
        float velocityScale = (finalMass > 0) ? 1f / Mathf.Pow(finalMass, 1f/3f) : 1f;
        
        Vector3 finalVelocity = velocity ?? Random.insideUnitCircle.normalized * 
            Random.Range(velocityRange.x, velocityRange.y) * velocityScale;

        Vector3 finalAngularVelocity = angularVelocity ?? new Vector3(
            Random.Range(spinRange.x, spinRange.y) * velocityScale,
            Random.Range(spinRange.x, spinRange.y) * velocityScale,
            Random.Range(spinRange.x, spinRange.y) * velocityScale
        );

        // Initialize the asteroid with the calculated properties
        asteroid.Initialize(
            randomMesh,
            finalMass,
            finalScale,
            finalVelocity,
            finalAngularVelocity
        );
        
        return asteroidGO;
    }

    public void ReleaseAsteroid(GameObject asteroidGO)
    {
        if (asteroidGO == null) return;
        asteroidPool.Release(asteroidGO);
    }

    // --------- ObjectPool Callbacks ---------
    private GameObject CreatePooledAsteroid()
    {
        RLog.Log("[Pool] Creating new pooled asteroid");
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
            float randomScaleFactor = Random.Range(massScaleRange.x, massScaleRange.y);
            float finalScale = Mathf.Pow(randomScaleFactor, 1f/3f);
            float finalMass = Asteroid.CalculateMassFromMesh(mesh, asteroid.Density, finalScale);
            return (finalMass, finalScale);
        }
    }

    private Mesh GetRandomMesh()
    {
        if (asteroidMeshes == null || asteroidMeshes.Length == 0)
        {
            return null;
        }
        return asteroidMeshes[Random.Range(0, asteroidMeshes.Length)];
    }
} 
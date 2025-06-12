using UnityEngine;
using UnityEngine.Pool;

public class AsteroidSpawner : MonoBehaviour
{
    public static AsteroidSpawner Instance { get; private set; }

    [Header("Asteroid Prefab")]
    [SerializeField] private GameObject asteroidPrefab;

    [Header("Asteroid Base Properties")]
    [SerializeField] private float density = 1f;
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
            Debug.LogError("Pooled object is missing Asteroid component.");
            asteroidPool.Release(asteroidGO);
            return null;
        }

        // --- Determine final properties before initialization ---
        Mesh randomMesh = GetRandomMesh();
        var (finalMass, finalScale) = CalculateMassAndScale(randomMesh, mass);

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
        
        AsteroidFieldManager.Instance.AddAsteroid(asteroidGO);
        return asteroidGO;
    }

    public void ReleaseAsteroid(GameObject asteroidGO)
    {
        if (asteroidGO == null) return;
        AsteroidFieldManager.Instance?.RemoveAsteroid(asteroidGO);
        asteroidPool.Release(asteroidGO);
    }

    // --------- ObjectPool Callbacks ---------
    private GameObject CreatePooledAsteroid()
    {
        Debug.Log("[Pool] Creating new pooled asteroid");
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
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void OnAsteroidDestroyed(GameObject asteroidGO)
    {
        Destroy(asteroidGO);
    }
    
    // we are either passing in a mass and we want to scale the mesh to that mass
    // or we pass in nothing and we want to pick a random scale, scale the mesh to that scale, and then calculate the mass
    private (float finalMass, float finalScale) CalculateMassAndScale(Mesh mesh, float? mass)
    {
        float baseMass = CalculateMeshMass(mesh);
        float massScaleFactor = mass.HasValue ? mass.Value / baseMass : Random.Range(massScaleRange.x, massScaleRange.y);
        float finalMass = baseMass * massScaleFactor;
        float finalScale = Mathf.Pow(massScaleFactor, 1/3f);
        return (finalMass, finalScale);
    }

    private float CalculateMeshMass(Mesh mesh)
    {
        if (mesh == null) return 1f;
        var bounds = mesh.bounds;
        float volume = bounds.size.x * bounds.size.y * bounds.size.z;
        return volume * density;
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
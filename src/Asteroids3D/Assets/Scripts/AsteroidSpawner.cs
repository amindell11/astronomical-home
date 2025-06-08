using UnityEngine;

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
    }

    public GameObject SpawnAsteroid(Pose pose, float? mass = null, Vector3? velocity = null, Vector3? angularVelocity = null)
    {
        if (asteroidPrefab == null)
        {
            Debug.LogError("Asteroid Prefab is not assigned in AsteroidSpawner.");
            return null;
        }

        GameObject asteroidGO = Instantiate(asteroidPrefab, pose.position, pose.rotation, transform.parent);
        Asteroid asteroid = asteroidGO.GetComponent<Asteroid>();

        if (asteroid == null)
        {
            Debug.LogError("Spawned object is missing Asteroid component.");
            Destroy(asteroidGO);
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
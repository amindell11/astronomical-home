using UnityEngine;
using System.Collections.Generic;

public class AsteroidFieldManager : MonoBehaviour
{
    public static AsteroidFieldManager Instance { get; private set; }

    [Header("Asteroid Population")]
    [SerializeField] private int maxAsteroids = 50;
    private List<GameObject> activeAsteroids = new List<GameObject>();

    [Header("Spawn Zone")]
    [SerializeField] private float minSpawnDistance = 15f;
    [SerializeField] private float maxSpawnDistance = 30f;
    [SerializeField] private float initMinSpawnDistance = 10f;

    [Header("Density Control")]
    [Tooltip("Target mass per square meter for the asteroid field.")]
    [SerializeField] private float targetDensity = 0.1f;
    [SerializeField] private float densityCheckRadius = 30f;
    [SerializeField] private int maxSpawnsPerFrame = 10;

    private Transform playerTransform;

    public List<GameObject> ActiveAsteroids => activeAsteroids;

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

    private void Start()
    {
        playerTransform = Camera.main.transform;
        if (playerTransform != null)
        {
            CheckAndSpawnAsteroids(initMinSpawnDistance, maxSpawnDistance);
        }
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            playerTransform = Camera.main.transform;
            if (playerTransform == null) return;
        }
        CheckAndSpawnAsteroids(minSpawnDistance, maxSpawnDistance);
    }

    private void CheckAndSpawnAsteroids(float minSpawn, float maxSpawn)
    {
        if (activeAsteroids.Count >= maxAsteroids) return;

        float currentDensity = GetFieldDensity(out float area);
        Debug.Log($"Current density: {currentDensity}, Target density: {targetDensity}, Area: {area}");
        if (currentDensity < targetDensity)
        {
            float massToSpawn = (targetDensity - currentDensity) * area;
            float massSpawned = 0f;
            int safetyBreak = maxSpawnsPerFrame; // Prevent potential infinite loops

            // Keep spawning until we've added enough mass, without exceeding total count.
            while (massSpawned < massToSpawn && activeAsteroids.Count < maxAsteroids && safetyBreak > 0)
            {
                Vector3 spawnPosition = playerTransform.position + (Vector3)(Random.insideUnitCircle.normalized * Random.Range(minSpawn, maxSpawn));
                Pose spawnPose = new Pose(spawnPosition, Random.rotationUniform);
                GameObject newAsteroidGO = AsteroidSpawner.Instance.SpawnAsteroid(spawnPose);
                if (newAsteroidGO == null) break;
                massSpawned += newAsteroidGO.GetComponent<Asteroid>().CurrentMass;
                safetyBreak--;
            }
        }
    }

    private float GetFieldDensity(out float area)
    {
        Vector2 checkCenter = (playerTransform != null) ? new Vector2(playerTransform.position.x, playerTransform.position.y) : new Vector2(transform.position.x, transform.position.y);
        int mask = 1 << LayerMask.NameToLayer("Asteroid");
        Collider[] results = Physics.OverlapSphere(checkCenter, densityCheckRadius, mask);
        float totalMassInRange = 0f;
        Debug.Log($"Found {results.Length} asteroids in range");
        foreach (Collider col in results)
        {
            Asteroid asteroid = col.GetComponent<Asteroid>();
            if (asteroid != null)
            {
                totalMassInRange += asteroid.CurrentMass;
            }
        }
        
        area = Mathf.PI * Mathf.Pow(densityCheckRadius, 2);
        return area > 0 ? totalMassInRange / area : 0f;
    }

    public void AddAsteroid(GameObject asteroid)
    {
        activeAsteroids.Add(asteroid);
    }

    public void RemoveAsteroid(GameObject asteroid)
    {
        activeAsteroids.Remove(asteroid);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = (playerTransform != null) ? playerTransform.position : transform.position;

        Gizmos.color = Color.cyan;
        // Draw a circle on the XY plane
        int segments = 32;
        float angle = 0f;
        Vector3 lastPoint = center + new Vector3(Mathf.Cos(angle) * densityCheckRadius, Mathf.Sin(angle) * densityCheckRadius, 0);
        for (int i = 1; i <= segments; i++)
        {
            angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * densityCheckRadius, Mathf.Sin(angle) * densityCheckRadius, 0);
            Gizmos.DrawLine(lastPoint, nextPoint);
            lastPoint = nextPoint;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, minSpawnDistance);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(center, initMinSpawnDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, maxSpawnDistance);
    }
} 
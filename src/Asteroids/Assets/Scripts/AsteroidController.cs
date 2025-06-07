using UnityEngine;
using System.Collections.Generic;

public class AsteroidController : MonoBehaviour
{
    // Singleton instance
    public static AsteroidController Instance { get; private set; }

    [Header("Asteroid Settings")]
    [SerializeField] private GameObject asteroidPrefab;
    [SerializeField] private int maxAsteroids = 50;

    [Header("Spawn Zone Settings")]
    [Tooltip("Minimum distance from player where asteroids can spawn")]
    [SerializeField] private float minSpawnDistance = 15f;
    [Tooltip("Maximum distance from player where asteroids can spawn")]

    [SerializeField] private float initMinSpawnDistance = 10f;
    [Tooltip("Maximum distance from player where asteroids can spawn at game start")]
    [SerializeField] private float maxSpawnDistance = 30f;
    [Tooltip("Distance at which asteroids are destroyed when too far from player")]

    [Header("Density Settings")]
    [SerializeField] private float targetDensity = 0.1f;
    [SerializeField] private CircleCollider2D densityCheckCollider;

    private Transform playerTransform;
    private List<GameObject> activeAsteroids = new List<GameObject>();
    private Asteroid asteroidTemplate;
    public List<GameObject> ActiveAsteroids => activeAsteroids;
    private void Awake()
    {
        // Singleton pattern implementation
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Get the asteroid template for range values
        if (asteroidPrefab != null)
        {
            asteroidTemplate = asteroidPrefab.GetComponent<Asteroid>();
        }
    }

    private void Start()
    {
        playerTransform = Camera.main.transform;
        CheckAndSpawnAsteroids(initMinSpawnDistance, maxSpawnDistance);
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            playerTransform = Camera.main.transform;
            return;
        }

        // Check current density and spawn if needed
        CheckAndSpawnAsteroids(minSpawnDistance, maxSpawnDistance);
    }

    private float GetAsteroidDensity(out float area)
    {
        int asteroidsInRange = 0;
        // Use OverlapCollider to get all asteroids in the density check area
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;
        filter.SetLayerMask(LayerMask.GetMask("Asteroid"));
        Collider2D[] results = new Collider2D[100];
        int count = densityCheckCollider.Overlap(filter, results);
        for (int i = 0; i < count; i++)
        {
            if (results[i] != null && results[i].CompareTag("Asteroid"))
            {
                asteroidsInRange++;
            }
        }
        area = Mathf.PI * densityCheckCollider.radius * densityCheckCollider.radius;
        return asteroidsInRange / area;
    }

    private void CheckAndSpawnAsteroids(float minSpawnDistance, float maxSpawnDistance)
    {
        if (activeAsteroids.Count >= maxAsteroids) return;

        float area;
        float currentDensity = GetAsteroidDensity(out area);

        // If density is below target, spawn new asteroids
        if (currentDensity < targetDensity)
        {
            int asteroidsToSpawn = Mathf.CeilToInt((targetDensity - currentDensity) * area);
            asteroidsToSpawn = Mathf.Min(asteroidsToSpawn, maxAsteroids - activeAsteroids.Count);

            for (int i = 0; i < asteroidsToSpawn; i++)
            {
                // Calculate random position in the spawn ring around the player
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float distance = Random.Range(minSpawnDistance, maxSpawnDistance);
                Vector2 randomCircle = new Vector2(
                    Mathf.Cos(angle) * distance,
                    Mathf.Sin(angle) * distance
                );

                Vector3 spawnPosition = new Vector3(
                    playerTransform.position.x + randomCircle.x,
                    playerTransform.position.y + randomCircle.y,
                    0f
                );
                // Use the helper method to spawn the asteroid
                SpawnAsteroid(spawnPosition);            
            }
        }
    }
    public GameObject SpawnAsteroid(Vector3 position, float? mass = null, Vector2? velocity = null, float? rotation = null, float? angularVelocity = null)
    {   
        GameObject asteroid = Instantiate(asteroidPrefab, position, Quaternion.Euler(0, 0, 0), transform);
        Asteroid asteroidComponent = asteroid.GetComponent<Asteroid>();
        if (asteroidComponent != null)
        {
            asteroidComponent.Initialize(position, mass, velocity, rotation, angularVelocity);
        }
        activeAsteroids.Add(asteroid);
        return asteroid;
    }
}
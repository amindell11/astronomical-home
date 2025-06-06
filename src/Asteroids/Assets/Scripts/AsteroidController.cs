using UnityEngine;
using System.Collections.Generic;

public class AsteroidController : MonoBehaviour
{
    [Header("Asteroid Settings")]
    [SerializeField] private GameObject asteroidPrefab;
    [SerializeField] private int maxAsteroids = 50;

    [Header("Spawn Zone Settings")]
    [Tooltip("Minimum distance from player where asteroids can spawn")]
    [SerializeField] private float minSpawnDistance = 15f;
    [Tooltip("Maximum distance from player where asteroids can spawn")]
    [SerializeField] private float maxSpawnDistance = 30f;
    [Tooltip("Distance at which asteroids are destroyed when too far from player")]
    [SerializeField] private float cullDistance = 40f;

    [Header("Density Settings")]
    [SerializeField] private float targetDensity = 0.1f;
    [SerializeField] private float densityCheckRadius = 20f;

    [Header("Asteroid Properties")]
    [SerializeField] private Sprite[] asteroidSprites;
    [SerializeField] private Vector2 sizeRange = new Vector2(0.5f, 2f);
    [SerializeField] private Vector2 velocityRange = new Vector2(0.5f, 2f);
    [SerializeField] private Vector2 spinRange = new Vector2(-30f, 30f);

    private Transform playerTransform;
    private List<GameObject> activeAsteroids = new List<GameObject>();

    private void Start()
    {
        playerTransform = Camera.main.transform;
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            playerTransform = Camera.main.transform;
            return;
        }

        // Check current density and spawn if needed
        CheckAndSpawnAsteroids();

        // Cull distant asteroids
        CullDistantAsteroids();
    }

    private void CheckAndSpawnAsteroids()
    {
        if (activeAsteroids.Count >= maxAsteroids) return;

        // Count asteroids within density check radius
        int asteroidsInRange = 0;
        foreach (var asteroid in activeAsteroids)
        {
            if (asteroid != null)
            {
                float distance = Vector2.Distance(playerTransform.position, asteroid.transform.position);
                if (distance <= densityCheckRadius)
                {
                    asteroidsInRange++;
                }
            }
        }

        // Calculate current density
        float area = Mathf.PI * densityCheckRadius * densityCheckRadius;
        float currentDensity = asteroidsInRange / area;

        // If density is below target, spawn new asteroids
        if (currentDensity < targetDensity)
        {
            int asteroidsToSpawn = Mathf.CeilToInt((targetDensity - currentDensity) * area);
            asteroidsToSpawn = Mathf.Min(asteroidsToSpawn, maxAsteroids - activeAsteroids.Count);

            for (int i = 0; i < asteroidsToSpawn; i++)
            {
                SpawnAsteroid();
            }
        }
    }

    private void SpawnAsteroid()
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

        // Instantiate asteroid as child of this controller
        GameObject asteroid = Instantiate(asteroidPrefab, spawnPosition, Quaternion.Euler(0, 0, Random.Range(0f, 360f)), transform);
        
        // Set random sprite if we have any
        if (asteroidSprites != null && asteroidSprites.Length > 0)
        {
            SpriteRenderer spriteRenderer = asteroid.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = asteroidSprites[Random.Range(0, asteroidSprites.Length)];
            }
        }
        
        // Set random size
        float size = Random.Range(sizeRange.x, sizeRange.y);
        asteroid.transform.localScale = new Vector3(size, size, size);

        // Add random velocity and set mass based on size
        Vector2 randomVelocity = Random.insideUnitCircle.normalized * Random.Range(velocityRange.x, velocityRange.y);
        Rigidbody2D rb = asteroid.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            // Mass scales with the cube of the size (volume)
            rb.mass = size * size * size;
            
            // Velocity scales inversely with size (larger asteroids move slower)
            randomVelocity /= size;
            rb.velocity = randomVelocity;
            
            // Angular velocity also scales inversely with size
            rb.angularVelocity = Random.Range(spinRange.x, spinRange.y) / size;
        }

        activeAsteroids.Add(asteroid);
    }

    private void CullDistantAsteroids()
    {
        for (int i = activeAsteroids.Count - 1; i >= 0; i--)
        {
            if (activeAsteroids[i] == null)
            {
                activeAsteroids.RemoveAt(i);
                continue;
            }

            float distance = Vector2.Distance(playerTransform.position, activeAsteroids[i].transform.position);
            if (distance > cullDistance)
            {
                Destroy(activeAsteroids[i]);
                activeAsteroids.RemoveAt(i);
            }
        }
    }
} 
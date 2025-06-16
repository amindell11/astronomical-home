using UnityEngine;
using System.Collections.Generic;

public class AsteroidFieldManager : MonoBehaviour
{
    public static AsteroidFieldManager Instance { get; private set; }

    [Header("Asteroid Population")]
    [SerializeField] private int maxAsteroids = 50;

    [Header("Spawn Zone")]
    [SerializeField] private float minSpawnDistance = 15f;
    [SerializeField] private float maxSpawnDistance = 30f;
    [SerializeField] private float initMinSpawnDistance = 10f;

    [Header("Volume Density Control")]
    [Tooltip("Target volume per square meter for the asteroid field (volume-based, not mass-based).")]
    [SerializeField] private float targetVolumeDensity = 0.1f;
    [SerializeField] private float densityCheckRadius = 30f;
    [SerializeField] private int maxSpawnsPerFrame = 10;
    
    [Header("Performance Optimization")]
    [SerializeField] private float densityCheckInterval = 0.25f;
    [SerializeField] private int maxOverlapResults = 50;

    private Transform playerTransform;
    private float cachedVolumeDensity;
    private float cachedArea;
    private Camera mainCamera;
    
    // Pre-allocated buffer for Physics.OverlapSphereNonAlloc
    private static Collider[] overlapBuffer;

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
        
        // Initialize static buffer if not already done
        if (overlapBuffer == null)
        {
            overlapBuffer = new Collider[maxOverlapResults];
        }
        mainCamera = Camera.main;
    }

    private void Start()
    {
        playerTransform = mainCamera.transform;
        if (playerTransform != null)
        {
            // Update cached density before initial spawn so volume calculations are correct
            UpdateCachedDensity();
            CheckAndSpawnAsteroids(initMinSpawnDistance, maxSpawnDistance, maxAsteroids);
        }
       // Debug.Log("========================initial asteroid field spawned");
        
        // Start the repeating asteroid field management
        InvokeRepeating(nameof(ManageAsteroidField), densityCheckInterval, densityCheckInterval);
    }

    private void ManageAsteroidField()
    {
        if (playerTransform == null)
        {
            playerTransform = mainCamera.transform;
            if (playerTransform == null) return;
        }
        
        UpdateCachedDensity();
        CheckAndSpawnAsteroids(minSpawnDistance, maxSpawnDistance, maxSpawnsPerFrame);
    }

    private void CheckAndSpawnAsteroids(float minSpawn, float maxSpawn, int maxSpawnsPerFrame)
    {
        if (AsteroidSpawner.Instance.ActiveAsteroidCount >= maxAsteroids) return;

        //Debug.Log($"Current volume density: {cachedVolumeDensity}, Target volume density: {targetVolumeDensity}, Area: {cachedArea}");
        
        if (cachedVolumeDensity < targetVolumeDensity)
        {
            float volumeToSpawn = (targetVolumeDensity - cachedVolumeDensity) * cachedArea;
            float volumeSpawned = 0f;
            int safetyBreak = maxSpawnsPerFrame; // Prevent potential infinite loops

            // Keep spawning until we've added enough volume, without exceeding total count.
            while (volumeSpawned < volumeToSpawn && AsteroidSpawner.Instance.ActiveAsteroidCount < maxAsteroids && safetyBreak > 0)
            { 
                // 1. pick a point with area-uniform probability in a unit disk
                Vector2 r = Random.insideUnitCircle;

                // 2. move it from 0–1 range to the desired annulus [min,max]
                float radius = Mathf.Lerp(minSpawn, maxSpawn, r.magnitude);
                Vector3 spawnOffset = new Vector3(r.x, 0f, r.y).normalized * radius;
                Vector3 spawnPosition = playerTransform.position + spawnOffset;
                Pose spawnPose = new Pose(spawnPosition, Random.rotationUniform);
                GameObject newAsteroidGO = AsteroidSpawner.Instance.SpawnAsteroid(spawnPose);
                if (newAsteroidGO == null) break;
                
                Asteroid asteroid = newAsteroidGO.GetComponent<Asteroid>();
                if (asteroid != null)
                {
                    volumeSpawned += asteroid.CurrentVolume;
                }
                safetyBreak--;
            }
        }
    }

    private void UpdateCachedDensity()
    {
        Vector3 checkCenter = (playerTransform != null) ? playerTransform.position : transform.position;
        checkCenter.y = 0f;
        int mask = 1 << LayerMask.NameToLayer("Asteroid");
        
        // Use non-allocating overlap sphere with pre-allocated buffer
        int numResults = Physics.OverlapSphereNonAlloc(checkCenter, densityCheckRadius, overlapBuffer, mask);
        float totalVolumeInRange = 0f;
        
        //Debug.Log($"Found {numResults} asteroids in range");
        for (int i = 0; i < numResults; i++)
        {
            Asteroid asteroid = overlapBuffer[i].GetComponent<Asteroid>();
            if (asteroid != null)
            {
                totalVolumeInRange += asteroid.CurrentVolume;
            }
        }
        
        cachedArea = Mathf.PI * Mathf.Pow(densityCheckRadius, 2);
        cachedVolumeDensity = cachedArea > 0 ? totalVolumeInRange / cachedArea : 0f;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = (playerTransform != null) ? playerTransform.position : transform.position;
        center.y = 0f;

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
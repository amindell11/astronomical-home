using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Shared asteroid field logic that can operate relative to any anchor transform.
/// Derive concrete managers (e.g. AsteroidFieldManager, SectorFieldManager) from this class
/// and implement <see cref="AcquireAnchor"/> to provide the reference point that
/// controls spawning and density calculations.
/// </summary>
public abstract class BaseFieldManager : MonoBehaviour
{
    [Header("Asteroid Population")]
    [SerializeField] protected int maxAsteroids = 50;

    [Header("Initial Spawn Zone")]
    [SerializeField] protected float maxSpawnDistance = 15f;
    [SerializeField] protected float minSpawnDistance = 10f;

    [Header("Volume Density Control")]
    [Tooltip("Target volume per square meter for the asteroid field (volume-based, not mass-based).")]
    [SerializeField] protected float targetVolumeDensity = 0.1f;
    [SerializeField] protected float densityCheckRadius = 30f;
    [SerializeField] protected int maxSpawnsPerFrame = 10;

    [Header("Performance Optimization")]
    [SerializeField] protected int maxOverlapResults = 50;

    // Runtime-computed cached values
    protected float cachedVolumeDensity;
    protected float cachedArea;

    // Buffer reused by OverlapSphereNonAlloc to avoid allocations each frame.
    private static Collider[] overlapBuffer;

    // The transform that represents the local origin of this asteroid field.
    protected Transform spawnAnchor;

    [Header("References")]
    [Tooltip("AsteroidSpawner used by this field. If null, will search parent hierarchy, then fall back to AsteroidSpawner.Instance.")]
    [SerializeField] private AsteroidSpawner spawnerOverride;

    protected AsteroidSpawner Spawner { get; private set; }

    /* ───────────────────────────── Unity Lifecycle ───────────────────────────── */
    protected virtual void Awake()
    {
        // Allocate a shared buffer once for all managers to reduce memory churn.
        if (overlapBuffer == null)
        {
            overlapBuffer = new Collider[maxOverlapResults];
        }

        // Early spawner resolution in case subclasses need it before Start()
        Spawner = spawnerOverride != null ? spawnerOverride : GetComponent<AsteroidSpawner>();
    }

    protected virtual void Start()
    {
        // Initial spawn uses the base class parameters
        ManageAsteroidField();
    }

    /* ───────────────────────────── Core Logic ───────────────────────────── */
    
    /// <summary>
    /// Main asteroid field management method. Uses default spawn parameters unless overridden.
    /// </summary>
    protected void ManageAsteroidField()
    {
        var spawnParams = GetSpawnParameters();
        ManageAsteroidField(spawnParams.minSpawn, spawnParams.maxSpawn, spawnParams.maxPerFrame);
    }

    /// <summary>
    /// Overloaded version that accepts explicit spawn parameters.
    /// </summary>
    private void ManageAsteroidField(float minSpawn, float maxSpawn, int maxPerFrame)
    {
        spawnAnchor = AcquireAnchor();
        if (spawnAnchor == null) return;

        UpdateCachedDensity();
        CheckAndSpawnAsteroids(minSpawn, maxSpawn, maxPerFrame);
    }

    /// <summary>
    /// Virtual method that subclasses can override to provide their own spawn parameters.
    /// By default, uses the base class values.
    /// </summary>
    protected virtual (float minSpawn, float maxSpawn, int maxPerFrame) GetSpawnParameters()
    {
        return (minSpawnDistance, maxSpawnDistance, maxSpawnsPerFrame);
    }

    protected abstract Transform AcquireAnchor();

    protected void CheckAndSpawnAsteroids(float minSpawn, float maxSpawn, int spawnsPerFrame)
    {
        if (Spawner == null) return;
        if (Spawner.ActiveAsteroidCount >= maxAsteroids) return;

        if (cachedVolumeDensity < targetVolumeDensity)
        {
            float volumeToSpawn = (targetVolumeDensity - cachedVolumeDensity) * cachedArea;
            float volumeSpawned = 0f;
            int safetyBreak = spawnsPerFrame;

            while (volumeSpawned < volumeToSpawn &&
                   Spawner.ActiveAsteroidCount < maxAsteroids &&
                   safetyBreak > 0)
            {
                // 1. Pick uniform-area point in a disk.
                Vector2 r = Random.insideUnitCircle;
                // 2. Map to desired annulus radius.
                float radius = Mathf.Lerp(minSpawn, maxSpawn, r.magnitude);
                Vector3 spawnOffset = new Vector3(r.x, 0f, r.y).normalized * radius;
                Vector3 spawnPosition = spawnAnchor.position + spawnOffset;
                Pose spawnPose = new Pose(spawnPosition, Random.rotationUniform);

                GameObject astGO = Spawner.SpawnAsteroid(spawnPose);
                if (astGO == null) break;

                Asteroid asteroid = astGO.GetComponent<Asteroid>();
                if (asteroid != null)
                {
                    volumeSpawned += asteroid.CurrentVolume;
                }
                safetyBreak--;
            }
        }
    }

    protected void UpdateCachedDensity()
    {
        Vector3 checkCenter = spawnAnchor != null ? spawnAnchor.position : transform.position;
        checkCenter.y = 0f;
        int mask = 1 << LayerMask.NameToLayer("Asteroid");

        int numResults = Physics.OverlapSphereNonAlloc(checkCenter, densityCheckRadius, overlapBuffer, mask);
        float totalVolumeInRange = 0f;
        for (int i = 0; i < numResults; i++)
        {
            Asteroid asteroid = overlapBuffer[i].GetComponent<Asteroid>();
            if (asteroid != null)
            {
                totalVolumeInRange += asteroid.CurrentVolume;
            }
        }

        cachedArea = Mathf.PI * densityCheckRadius * densityCheckRadius;
        cachedVolumeDensity = cachedArea > 0 ? totalVolumeInRange / cachedArea : 0f;
    }

#if UNITY_EDITOR
    protected virtual void OnDrawGizmosSelected()
    {
        Transform anchor = spawnAnchor != null ? spawnAnchor : AcquireAnchor();
        if (anchor == null) return;

        Vector3 center = anchor.position;
        center.y = 0f;

        Gizmos.color = Color.cyan;
        int segments = 32;
        float angle = 0f;
        Vector3 lastPoint = center + new Vector3(Mathf.Cos(angle) * densityCheckRadius, 0, Mathf.Sin(angle) * densityCheckRadius);
        for (int i = 1; i <= segments; i++)
        {
            angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * densityCheckRadius, 0, Mathf.Sin(angle) * densityCheckRadius);
            Gizmos.DrawLine(lastPoint, nextPoint);
            lastPoint = nextPoint;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, minSpawnDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, maxSpawnDistance);
    }
#endif
} 
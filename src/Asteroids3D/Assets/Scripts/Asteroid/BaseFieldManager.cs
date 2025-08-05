using Editor;
using UnityEngine;

namespace Asteroid
{
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

        public float TargetDensity { get => targetVolumeDensity; set => targetVolumeDensity = value; }

        // Runtime-computed cached values
        protected float cachedVolumeDensity;
        protected float cachedArea;

        // The transform that represents the local origin of this asteroid field.
        protected Transform spawnAnchor;

        [Header("References")]
        [Tooltip("AsteroidSpawner used by this field. If null, will search parent hierarchy, then fall back to AsteroidSpawner.Instance.")]
        [SerializeField] private AsteroidSpawner spawnerOverride;
    
        [Tooltip("Collider that defines the culling boundary for the asteroid field. If null, will search in children.")]
        [SerializeField] private SphereCollider cullingBoundaryCollider;

        protected AsteroidSpawner Spawner { get; private set; }

        /* ───────────────────────────── Unity Lifecycle ───────────────────────────── */
        protected virtual void Awake()
        {
            // Early spawner resolution in case subclasses need it before Start()
            Spawner = spawnerOverride != null ? spawnerOverride : GetComponent<AsteroidSpawner>();
        
            // Initialize culling boundary collider if not set
            if (cullingBoundaryCollider == null)
            {
                cullingBoundaryCollider = GetComponentInChildren<SphereCollider>();
            }
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
            ManageAsteroidField(minSpawnDistance, maxSpawnDistance, maxAsteroids);
        }

        /// <summary>
        /// Overloaded version that accepts explicit spawn parameters.
        /// </summary>
        protected void ManageAsteroidField(float minSpawn, float maxSpawn, int maxPerFrame)
        {
            spawnAnchor = AcquireAnchor();
            if (spawnAnchor == null) return;

            UpdateCachedDensity();
            CheckAndSpawnAsteroids(minSpawn, maxSpawn, maxPerFrame);
        }

        protected abstract Transform AcquireAnchor();

        protected void CheckAndSpawnAsteroids(float minSpawn, float maxSpawn, int spawnsPerFrame)
        {
            if (Spawner == null) 
            {
                return;
            }
        
            if (Spawner.ActiveAsteroidCount >= maxAsteroids) 
            {
                return;
            }
        
            if (cachedVolumeDensity < targetVolumeDensity)
            {
                float volumeToSpawn = (targetVolumeDensity - cachedVolumeDensity) * cachedArea;
                RLog.Asteroid($"BaseFieldManager: SPAWNING NEEDED | Volume deficit: {volumeToSpawn:F2} | Will spawn up to {spawnsPerFrame} asteroids");
            
                float volumeSpawned = 0f;
                int actualSpawns = 0;
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
                    GameObject astGO = Spawner.SpawnAsteroid(AsteroidSpawnRequest.Random(spawnPose));
                    if (astGO == null) 
                    {
                        break;
                    }

                    Asteroid asteroid = astGO.GetComponent<Asteroid>();
                    if (asteroid != null)
                    {
                        volumeSpawned += asteroid.CurrentVolume;
                        actualSpawns++;
                    }
                    safetyBreak--;
                }
            
                RLog.Asteroid($"BaseFieldManager: SPAWN COMPLETE | Spawned {actualSpawns} asteroids | Volume spawned: {volumeSpawned:F2} | Target was: {volumeToSpawn:F2} | Safety break remaining: {safetyBreak}");
            }
            else
            {
                RLog.Asteroid($"BaseFieldManager: Density sufficient ({cachedVolumeDensity:F4} >= {targetVolumeDensity:F4}) - no spawning needed");
            }
        }

        protected void UpdateCachedDensity()
        {
            if (Spawner == null)
            {
                cachedVolumeDensity = 0;
                cachedArea = 0;
                return;
            }

            cachedArea = Mathf.PI * densityCheckRadius * densityCheckRadius;
            cachedVolumeDensity = cachedArea > 0 ? Spawner.TotalActiveVolume / cachedArea : 0f;
        
            RLog.Asteroid($"BaseFieldManager: DENSITY UPDATE | Active Volume: {Spawner.TotalActiveVolume:F2} | Check Area: {cachedArea:F2} | Density: {cachedVolumeDensity:F4} | Target: {targetVolumeDensity:F4} | Active Count: {Spawner.ActiveAsteroidCount}");
        }

        /// <summary>
        /// Sets the field size and updates the culling boundary collider accordingly.
        /// </summary>
        /// <param name="radius">The radius for the asteroid field</param>
        public virtual void SetFieldSize(float radius)
        {
            if (Spawner == null) return;
        
            densityCheckRadius = radius;
            maxSpawnDistance = radius;
        
            // Update culling boundary collider with a small margin
            if (cullingBoundaryCollider != null)
            {
                float marginMultiplier = 1.1f; // 10% margin
                float cullingRadius = maxSpawnDistance * marginMultiplier;
                cullingBoundaryCollider.radius = cullingRadius;
            }
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
} 
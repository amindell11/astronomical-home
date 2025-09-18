using Editor;
using Game;
using UnityEngine;

namespace Asteroids
{
    /// <summary>
    /// Shared asteroid field logic that can operate relative to any anchor transform.
    /// Derive concrete managers (e.g. AsteroidFieldManager, SectorFieldManager) from this class
    /// and implement <see cref="AcquireAnchor"/> to provide the reference point that
    /// controls spawning and density calculations.
    /// </summary>
    ///
    [RequireComponent(typeof(SphereCollider))]
    public class Field : MonoBehaviour
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
        private float cachedVolumeDensity;
        private float cachedArea;

        // The transform that represents the local origin of this asteroid field.
        private SphereCollider cullingBoundaryCollider;
        [Header("References")]
        [Tooltip("AsteroidSpawner used by this field. If null, will search parent hierarchy, then fall back to AsteroidSpawner.Instance.")]
        [SerializeField] private Spawner spawnerOverride;
        

        protected Spawner Spawner { get; private set; }
        protected Vector3 SpawnCenter;
        protected virtual void Awake()
        {
            Spawner = spawnerOverride ? spawnerOverride : GetComponent<Spawner>();
            cullingBoundaryCollider = GetComponentInChildren<SphereCollider>();
        }

        protected virtual void Start()
        {
            ManageField();
        }

    
        /// <summary>
        /// Main asteroid field management method. Uses default spawn parameters unless overridden.
        /// </summary>
        protected void ManageField()
        {
            ManageField(minSpawnDistance, maxSpawnDistance, maxAsteroids);
        }

        /// <summary>
        /// Overloaded version that accepts explicit spawn parameters.
        /// </summary>
        protected void ManageField(float minSpawn, float maxSpawn, int maxPerFrame)
        {
            UpdateCachedDensity();
            CheckAndSpawnAsteroids(minSpawn, maxSpawn, maxPerFrame);
        }

        protected void CheckAndSpawnAsteroids(float minSpawn, float maxSpawn, int spawnsPerFrame)
        {
            if (!Spawner || Spawner.ActiveAsteroidCount >= maxAsteroids) return;
            if (cachedVolumeDensity < targetVolumeDensity)
            {            
                float volumeToSpawn = (targetVolumeDensity - cachedVolumeDensity) * cachedArea;
                RLog.Asteroid($"BaseFieldManager: SPAWNING NEEDED | Volume deficit: {volumeToSpawn:F2} | Will spawn up to {spawnsPerFrame} asteroids");
                float volumeSpawned = 0f;
                int spawns = 0;
                int safetyBreak = spawnsPerFrame;
                
                while (volumeSpawned < volumeToSpawn &&
                       Spawner.ActiveAsteroidCount < maxAsteroids &&
                       safetyBreak > 0)
                {
                    float r = Mathf.Lerp(minSpawn, maxSpawn, Random.insideUnitCircle.magnitude);
                    var offset = GamePlane.ProjectOntoPlane(Random.insideUnitSphere.normalized) * r;
                    var pos = SpawnCenter + offset;
                    var fullPose = new Pose(pos, Random.rotationUniform);
                    var ast = Spawner.SpawnAsteroid(SpawnRequest.Random(fullPose));
                    if (!ast) break;
                    var asteroid = ast.GetComponent<Asteroid>();
                    if (asteroid)
                    {
                        volumeSpawned += asteroid.CurrentVolume;
                        spawns++;
                    }
                    safetyBreak--;
                }
                RLog.Asteroid($"BaseFieldManager: SPAWN COMPLETE | Spawned {spawns} asteroids | Volume spawned: {volumeSpawned:F2} | Target was: {volumeToSpawn:F2} | Safety break remaining: {safetyBreak}");
            }
            else
            {
                RLog.Asteroid($"BaseFieldManager: Density sufficient ({cachedVolumeDensity:F4} >= {targetVolumeDensity:F4}) - no spawning needed");
            }
        }

        protected void UpdateCachedDensity()
        {
            if (!Spawner)
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
            densityCheckRadius = radius;
            maxSpawnDistance = radius;
            if (!cullingBoundaryCollider) return;
            const float marginMultiplier = 1.1f; // 10% margin
            float cullingRadius = maxSpawnDistance * marginMultiplier;
            cullingBoundaryCollider.radius = cullingRadius;
        }

#if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {

            var center = SpawnCenter;
            center.y = 0f;

            Gizmos.color = Color.cyan;
            const int segments = 32;
            float angle = 0f;
            var lastPoint = center + new Vector3(Mathf.Cos(angle) * densityCheckRadius, 0, Mathf.Sin(angle) * densityCheckRadius);
            for (int i = 1; i <= segments; i++)
            {
                angle = (i / (float)segments) * Mathf.PI * 2f;
                var nextPoint = center + new Vector3(Mathf.Cos(angle) * densityCheckRadius, 0, Mathf.Sin(angle) * densityCheckRadius);
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
using Game;
using UnityEngine;

namespace Asteroids.Fields
{
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

        private float cachedVolumeDensity;
        private float cachedArea;

        private SphereCollider cullingBoundaryCollider;

        protected Spawner Spawner { get; private set; }
        protected Vector3 SpawnCenter;
        protected virtual void Awake()
        {
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
            if (!Spawner || Registry.Instance.ActiveCount >= maxAsteroids) return;
            if (cachedVolumeDensity < targetVolumeDensity)
            {            
                float volumeToSpawn = (targetVolumeDensity - cachedVolumeDensity) * cachedArea;
                float volumeSpawned = 0f;
                int spawns = 0;
                int safetyBreak = spawnsPerFrame;
                
                while (volumeSpawned < volumeToSpawn &&
                       Registry.Instance.ActiveCount < maxAsteroids &&
                       safetyBreak > 0)
                {
                    float r = Mathf.Lerp(minSpawn, maxSpawn, Random.insideUnitCircle.magnitude);
                    var offset = GamePlane.ProjectOntoPlane(Random.insideUnitSphere.normalized) * r;
                    var pos = SpawnCenter + offset;
                    var fullPose = new Pose(pos, Random.rotationUniform);
                    var ast = Spawner.SpawnAsteroid(SpawnRequest.Random(fullPose));
                    if (ast)
                    {
                        volumeSpawned += ast.Volume;
                        spawns++;
                    }
                    safetyBreak--;
                }
            }
        }

        private void UpdateCachedDensity()
        {
            cachedArea = Mathf.PI * densityCheckRadius * densityCheckRadius;
            cachedVolumeDensity = cachedArea > 0 ? Registry.Instance.TotalVolume / cachedArea : 0f;
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
using Game;
using Asteroids.Fragnetics;
using Asteroids.Spawning;
using UnityEngine.Pool;
using UnityEngine;
using Utils;

namespace Asteroids.Fields
{
    public partial class AsteroidField : MonoBehaviour
    {
        protected const float BoundaryMargin = 1.1f;
        
        [SerializeField] protected AsteroidFieldSettings settings;
        [SerializeField] private SphereCollider cullingBoundary;

        protected AsteroidSpawner AsteroidSpawner { get; private set; }
        protected Vector3 SpawnCenter;
        protected float TargetVolume;
        protected SphereCollider CullingBoundary;
        
        // Cached settings for quick access
        protected int maxAsteroids;
        protected float minSpawnDistance;
        protected float maxSpawnDistance;
        protected float targetVolumeDensity;
        protected float densityCheckRadius;
        protected int maxSpawnsPerFrame;
        
        protected virtual void Awake()
        {
            gameObject.tag = TagNames.AsteroidField;
            AsteroidSpawner = GetComponent<AsteroidSpawner>() ?? gameObject.AddComponent<AsteroidSpawner>();
            SpawnCenter = transform.position;
            CacheSettings();
        }
        
        protected virtual void CacheSettings()
        {
            if (!settings)
            {
                return;
            }
            
            maxAsteroids = settings.maxAsteroids;
            minSpawnDistance = settings.initialMinSpawnDistance;
            maxSpawnDistance = settings.initialMaxSpawnDistance;
            targetVolumeDensity = settings.targetVolumeDensity;
            densityCheckRadius = settings.densityCheckRadius;
            maxSpawnsPerFrame = settings.maxSpawnsPerFrame;
        }

        public void SetWorldAnchor(Transform anchor) => AsteroidSpawner?.SetWorldAnchor(anchor);

        public void DespawnAll() => AsteroidSpawner?.DespawnAll();

        protected virtual void Start()
        {
            CullingBoundary = cullingBoundary;
            if (CullingBoundary) CullingBoundary.radius = maxSpawnDistance * BoundaryMargin;
            RecalculateTargetVolume();
            ManageField();
        }
        protected void ManageField()
        {
            ManageField(minSpawnDistance, maxSpawnDistance, maxAsteroids);
        }
        protected void ManageField(float minSpawn, float maxSpawn, int maxPerFrame)
        {
            CheckAndSpawnAsteroids(minSpawn, maxSpawn, maxPerFrame);
        }
        private void CheckAndSpawnAsteroids(float minSpawn, float maxSpawn, int spawnsPerFrame)
        {
            if (!AsteroidSpawner) return;
            var safetyBreak = spawnsPerFrame;
            while (AsteroidSpawner.TotalVolume < TargetVolume &&
                   AsteroidSpawner.ActiveCount < maxAsteroids &&
                   safetyBreak > 0)
            {
                var pos = GetRandomFieldPos(minSpawn, maxSpawn);
                var rot = Random.rotationUniform;
                var attrs = AsteroidSpawner.AttributeProvider.Roll();
                AsteroidSpawner.Spawn(new Pose(pos, rot), attrs);
                safetyBreak--;
            }
        }

        private Vector3 GetRandomFieldPos(float minSpawn, float maxSpawn)
        {
            var dir = GamePlane.PlaneDirToWorld(Random.insideUnitCircle.normalized);
            var r = Mathf.Sqrt(Mathf.Lerp(minSpawn * minSpawn, maxSpawn * maxSpawn, Random.value));
            return SpawnCenter + dir * r;
        }
        
        private void RecalculateTargetVolume()
        {
            TargetVolume = targetVolumeDensity * Mathf.PI * densityCheckRadius * densityCheckRadius;
        }
    }
} 
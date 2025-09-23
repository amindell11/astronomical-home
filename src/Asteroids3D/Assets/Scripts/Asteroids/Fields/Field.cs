using Game;
using Asteroids.Fragnetics;
using Asteroids.Spawning;
using UnityEditor.SceneManagement;
using UnityEngine.Pool;
using UnityEngine;
using Utils;

namespace Asteroids.Fields
{
    public class Field : MonoBehaviour
    {
        protected const float BoundaryMargin = 1.1f;
        
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

        protected Spawner Spawner { get; private set; }
        protected Vector3 SpawnCenter;
        protected float TargetVolume;
        protected SphereCollider CullingBoundary;
        protected virtual void Awake()
        {
            gameObject.tag = TagNames.AsteroidField;
            CullingBoundary = GameObject.FindGameObjectWithTag(TagNames.AsteroidCullingBoundary).GetComponent<SphereCollider>();
            Spawner = GetComponent<Spawner>() ?? gameObject.AddComponent<Spawner>();
            SpawnCenter = transform.position;
            CullingBoundary.radius = maxSpawnDistance * BoundaryMargin;
        }

        protected virtual void Start()
        {
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
            if (!Spawner) return;
            int safetyBreak = spawnsPerFrame;
            while (Spawner.Registry.TotalVolume < TargetVolume &&
                   Spawner.Registry.ActiveCount < maxAsteroids &&
                   safetyBreak > 0)
            {
                var pos = GetRandomFieldPos(minSpawn, maxSpawn);
                var rot = Random.rotationUniform;
                Spawner.SpawnRandom(new Pose(pos,rot));
                safetyBreak--;
            }
        }

        private Vector3 GetRandomFieldPos(float minSpawn, float maxSpawn)
        {
            var dir = GamePlane.PlaneDirToWorld(Random.insideUnitCircle.normalized);
            float r = Mathf.Sqrt(Mathf.Lerp(minSpawn * minSpawn, maxSpawn * maxSpawn, Random.value));
            return SpawnCenter + dir * r;
        }
        
        private void RecalculateTargetVolume()
        {
            TargetVolume = targetVolumeDensity * Mathf.PI * densityCheckRadius * densityCheckRadius;
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
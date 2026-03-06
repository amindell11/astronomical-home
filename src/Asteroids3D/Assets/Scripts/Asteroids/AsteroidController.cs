using System;
using Asteroids.Fragnetics;
using Asteroids.Spawning;
using Diagnostics.Performance;
using Game;
using UnityEngine;
using Utils;

namespace Asteroids
{
    [RequireComponent(typeof(AsteroidDamage))]
    public partial class AsteroidController : MonoBehaviour
    {
        private MeshFilter meshFilter;
        private MeshCollider meshCollider;
        private SphereCollider cheapCollider;
        private Transform worldFollowTransform;
        private AsteroidDamage damage;


        [Header("Performance Tuning")]
        [Tooltip("Distance at which the detailed MeshCollider becomes active (units)")]
        [SerializeField] private float detailedColliderEnableDistance = 75f;

        private Vector3 initialVelocity;
        private Vector3 initialAngularVelocity;
        public float Mass => Rb.mass;
        public float Volume { get; private set; }
        public Rigidbody Rb { get; private set; }
        public AsteroidSpawner AsteroidSpawner { get; private set; }
        public Renderer Renderer { get; private set; }
        public Fragger Fragger { get; private set; }
        public Mesh CurrentMesh => meshFilter.sharedMesh;
        public event Action<Vector3> OnDestroyed;


        private void Awake()
        {
            Rb = GetComponent<Rigidbody>();
            meshFilter = GetComponent<MeshFilter>();
            meshCollider = GetComponent<MeshCollider>();
            cheapCollider = GetComponent<SphereCollider>();
            Renderer = GetComponent<Renderer>();
            damage = GetComponent<AsteroidDamage>();
            Rb.useGravity = false;
        }

        public void SetWorldAnchor(Transform anchor)
        {
            worldFollowTransform = anchor;
        }

        public void Initialize(
            AsteroidSpawner asteroidSpawner,
            Fragger fragger,
            AsteroidSpawnSettings.MeshInfo meshInfo,
            float mass,
            float scale,
            Vector3 velocity,
            Vector3 angularVelocity
        )
        {
            var prevAutoSync = Physics.autoSyncTransforms;
            Physics.autoSyncTransforms = false;

            meshFilter.mesh = meshInfo.mesh;
            AsteroidSpawner = asteroidSpawner;
            Fragger = fragger;

            Volume = meshInfo.cachedVolume * (scale * scale * scale);

            Rb.mass = mass;
            transform.localScale = Vector3.one * scale;

            Rb.linearVelocity = velocity;
            Rb.angularVelocity = angularVelocity;

            UpdateMeshCollider(meshInfo);

            if (cheapCollider)
            {
                var size = meshInfo.mesh.bounds.size;
                var radius = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * scale * 0.5f;
                cheapCollider.radius = radius;
            }

            initialVelocity = velocity;
            initialAngularVelocity = angularVelocity;

            damage?.Initialize(Volume);

            Physics.SyncTransforms();
            Physics.autoSyncTransforms = prevAutoSync;
        }
        public void ResetAsteroid()
        {
            UpdateKinematics(initialVelocity, initialAngularVelocity);
            damage?.ResetDamage(Volume);
        }

        public void UpdateKinematics(Vector3 vel, Vector3 spin)
        {
            Rb.linearVelocity = vel;
            Rb.angularVelocity = spin;
        }

        private void UpdateMeshCollider(AsteroidSpawnSettings.MeshInfo meshInfo)
        {
            if (!meshCollider) return;
            var targetColliderMesh = meshInfo.colliderMesh ? meshInfo.colliderMesh : meshInfo.mesh;
            if (meshCollider.sharedMesh != targetColliderMesh)
                meshCollider.sharedMesh = targetColliderMesh;
            meshCollider.enabled = false;
        }

        internal void HandleDestroyed(HitData hit)
        {
            Fragger.CreateFragments(this, hit, _ => CleanupAsteroid());
            OnDestroyed?.Invoke(transform.position);
        }

        private void CleanupAsteroid()
        {
            Rb.linearVelocity = Vector3.zero;
            Rb.angularVelocity = Vector3.zero;
            AsteroidSpawner.Despawn(this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(TagNames.AsteroidCullingBoundary))
                CleanupAsteroid();
        }

        private void OnCollisionEnter(Collision collision)
        {
            damage?.HandleCollision(collision);
        }

        private void LateUpdate()
        {
            using (LatencyProfilingMarkers.Measure(FrameTimingAccumulator.Category.AsteroidUpdate, LatencyProfilingMarkers.AsteroidUpdate))
            {
                PlaneConstraints.ConstrainPosition(transform);

                if (!meshCollider) return;
                if (!worldFollowTransform) return;
                var distSqr = (GamePlane.ProjectOntoPlane(worldFollowTransform.position) -
                               GamePlane.ProjectOntoPlane(transform.position)).sqrMagnitude;
                var shouldEnable = distSqr < detailedColliderEnableDistance * detailedColliderEnableDistance;
                if (meshCollider.enabled != shouldEnable)
                    meshCollider.enabled = shouldEnable;
            }
        }
    }
}

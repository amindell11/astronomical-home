using System;
using Asteroids.Fragnetics;
using Asteroids.Spawning;
using Game;
using UnityEngine;
using Utils;

namespace Asteroids
{
    [RequireComponent(typeof(AsteroidDamage))]
    [RequireComponent(typeof(SphereCollider))]
    public class AsteroidController : MonoBehaviour
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
        public float Radius { get; private set; }
        /// <summary>Baked mesh-local covering spheres (1..3) for this asteroid's mesh, or null
        /// when the mesh has no multi-lobe bake (≤1 lobe) — downstream falls back to the single
        /// <see cref="Radius"/> circle. Rides in from the shared <see cref="AsteroidSpawnSettings.MeshInfo"/>.</summary>
        public AsteroidSpawnSettings.MeshInfo.LobeSphere[] Lobes { get; private set; }
        public Rigidbody Rb { get; private set; }
        public AsteroidSpawner AsteroidSpawner { get; private set; }
        public Renderer Renderer { get; private set; }
        public Fragger Fragger { get; private set; }
        public AsteroidDamage Damage => damage;
        /// <summary>Always-present cheap collider (attached to the rigidbody). Handed to
        /// obstacle-scan consumers that resolve mass/root through a collider reference.</summary>
        public Collider SimpleCollider => cheapCollider;
        public Mesh CurrentMesh => meshFilter.sharedMesh;
        public event Action<Vector3> OnDestroyed;
        public event Action OnInitialized;


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
            Vector3 angularVelocity,
            float lethality = 1f
        )
        {
            var prevAutoSync = Physics.autoSyncTransforms;
            Physics.autoSyncTransforms = false;

            meshFilter.mesh = meshInfo.mesh;
            AsteroidSpawner = asteroidSpawner;
            Fragger = fragger;
            Lobes = meshInfo.cachedLobes is { Length: > 0 } lobes ? lobes : null;

            Volume = meshInfo.cachedVolume * (scale * scale * scale);

            Rb.mass = mass;
            transform.localScale = Vector3.one * scale;

            Rb.linearVelocity = velocity;
            Rb.angularVelocity = angularVelocity;

            UpdateMeshCollider(meshInfo);

            // Stays local-space: the transform already carries `scale`, and SphereCollider
            // .radius is scaled by it again — folding scale in here makes the world sphere
            // and Radius come out scale² too big.
            var localRadius = AsteroidGeometry.RadiusFromVolume(meshInfo.cachedVolume);
            cheapCollider.radius = localRadius;
            Radius = localRadius * transform.lossyScale.x;

            initialVelocity = velocity;
            initialAngularVelocity = angularVelocity;

            damage?.Initialize(Volume, lethality);

            Physics.SyncTransforms();
            Physics.autoSyncTransforms = prevAutoSync;

            OnInitialized?.Invoke();
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

        private void OnCollisionEnter(Collision collision)
        {
            damage?.HandleCollision(collision);
        }

        private void LateUpdate()
        {
            PlaneConstraints.ConstrainPosition(transform);

            if (!meshCollider) return;
            // No anchor = no collider LOD: full physics, never ghosts (harness/spectator/benchmark fields).
            var shouldEnable = true;
            if (worldFollowTransform)
            {
                var distSqr = (GamePlane.ProjectOntoPlane(worldFollowTransform.position) - GamePlane.ProjectOntoPlane(transform.position)).sqrMagnitude;
                shouldEnable = distSqr < detailedColliderEnableDistance * detailedColliderEnableDistance;
            }
            if (meshCollider.enabled != shouldEnable)
                meshCollider.enabled = shouldEnable;
        }
    }
}

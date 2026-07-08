using System;
using System.Collections.Generic;
using Asteroids.Fragnetics;
using Asteroids.Spawning;
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
        public float Radius { get; private set; }
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
                // Local-space radius: SphereCollider.radius is scaled by the transform, and
                // the transform already carries `scale` — baking it in here too made the
                // world sphere (and the reported Radius) grow/shrink by scale² instead of
                // scale: the cheap sphere and the AI-facing Radius were both wrong by a
                // factor of scale.
                //
                // The radius statistic is the MEAN vertex distance, not the circumscribed
                // max: rocks are irregular, and a sphere that circumscribes the longest
                // protrusion gives everything (AI avoidance rings included) far too much
                // berth. Occasional clipping of a protrusion beats phantom volume.
                //
                // The cheap sphere (culling-boundary + far-field self-collision trigger; ship
                // impacts use the detailed MeshCollider) and the AI-facing Radius are fed the
                // same value here by choice, not necessity — they may legitimately diverge
                // later (e.g. a looser cull sphere vs a tighter avoidance radius).
                var localRadius = MeanVertexRadius(meshInfo.mesh);
                cheapCollider.radius = localRadius;
                Radius = localRadius * transform.lossyScale.x;
            }

            initialVelocity = velocity;
            initialAngularVelocity = angularVelocity;

            damage?.Initialize(Volume);

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

        // Baked once per shared mesh (meshes are shared assets — a handful per settings).
        private static readonly Dictionary<Mesh, float> MeanRadiusCache = new();

        /// <summary>
        /// Mean distance of the mesh's vertices from its local origin — the "typical"
        /// silhouette radius of an irregular rock, rotation-agnostic (asteroids tumble in
        /// 3D, so a per-axis or in-plane measure would drift as they rotate). Deliberately
        /// tighter than the circumscribed radius; protrusions may clip.
        /// </summary>
        internal static float MeanVertexRadius(Mesh mesh)
        {
            if (MeanRadiusCache.TryGetValue(mesh, out var cached)) return cached;

            var vertices = mesh.vertices;
            var sum = 0f;
            for (var i = 0; i < vertices.Length; i++)
                sum += vertices[i].magnitude;
            var mean = vertices.Length > 0 ? sum / vertices.Length : mesh.bounds.extents.magnitude;
            MeanRadiusCache[mesh] = mean;
            return mean;
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

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only visualization of the baked lobe decomposition for the current
        /// mesh. Recomputes lobes on the fly from the same deterministic baker (reads
        /// nothing at runtime); draws each as a world-space wire sphere. Only when
        /// selected, so it stays cheap.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            var mf = GetComponent<MeshFilter>();
            var mesh = mf != null ? mf.sharedMesh : null;
            if (mesh == null) return;

            var lobes = AsteroidLobeBaker.Bake(mesh, out _);
            if (lobes == null) return;

            Gizmos.color = new Color(0.25f, 0.9f, 1f, 0.7f);
            float scale = transform.lossyScale.x;
            foreach (var lobe in lobes)
                Gizmos.DrawWireSphere(transform.TransformPoint(lobe.center), lobe.radius * scale);
        }
#endif

        private void LateUpdate()
        {
            PlaneConstraints.ConstrainPosition(transform);

            if (!meshCollider) return;
            if (!worldFollowTransform) return;
            var distSqr = (GamePlane.ProjectOntoPlane(worldFollowTransform.position) - GamePlane.ProjectOntoPlane(transform.position)).sqrMagnitude;
            var shouldEnable = distSqr < detailedColliderEnableDistance * detailedColliderEnableDistance;
            if (meshCollider.enabled != shouldEnable)
                meshCollider.enabled = shouldEnable;

        }
    }
}

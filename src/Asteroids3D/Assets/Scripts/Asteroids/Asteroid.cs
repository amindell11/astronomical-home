using System;
using Asteroids.Fragnetics;
using Asteroids.Spawning;
using Damage;
using Editor;
using Game;
using UnityEngine;
using Utils;

namespace Asteroids
{
    public class Asteroid : MonoBehaviour, IDamageable
    {
        [Header("Visual/Audio Effects")]
        [SerializeField] private GameObject explosionPrefab;
        [SerializeField] private AudioClip explosionSound;
        [SerializeField] private float explosionVolume = 0.7f;
    
        [Header("Damage Tuning")]
        [SerializeField]
        [Tooltip("Multiplier that converts collision energy (J) to gameplay damage.")]
        private float energyToDamageScale = 0.01f;

        [Header("Damage Soft Cap")]
        [SerializeField, Tooltip("Damage below this value is unaffected; excess is softened")] 
        private float softCapThreshold = 50f;

        [SerializeField, Range(0.1f, 1f), Tooltip("Exponent applied to damage above the soft-cap threshold (0.5 = sqrt, 1 = no cap)")]
        private float softCapExponent = 0.5f;

        private float ApplySoftCap(float damage)
        {
            if (damage <= softCapThreshold) return damage;
            var excess = damage - softCapThreshold;
            return softCapThreshold + Mathf.Pow(excess, softCapExponent);
        }

        [Header("Health")]
        [SerializeField]
        [Tooltip("Base health per unit volume. Total health = volume * this value.")]
        private float healthPerUnitVolume = 10f;

        private MeshFilter meshFilter;
        private MeshCollider meshCollider;
        private SphereCollider cheapCollider;
        private Transform mainCameraTransform;


        [Header("Performance Tuning")]
        [Tooltip("Distance at which the detailed MeshCollider becomes active (units)")]
        [SerializeField] private float detailedColliderEnableDistance = 75f;

        private Vector3 initialVelocity;
        private Vector3 initialAngularVelocity;
        public float Mass => Rb.mass;
        public float Volume { get; private set; }
        public Rigidbody Rb { get; private set; }
        public Spawner Spawner { get; private set; }
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public Renderer Renderer { get; private set; }
        public Mesh CurrentMesh => meshFilter.sharedMesh;


        private void Awake()
        {
            Rb = GetComponent<Rigidbody>();
            meshFilter = GetComponent<MeshFilter>();
            meshCollider = GetComponent<MeshCollider>();
            cheapCollider = GetComponent<SphereCollider>();
            Renderer = GetComponent<Renderer>();
            mainCameraTransform = Camera.main ? Camera.main.transform : null;
            Rb.useGravity = false;
        }

        private void OnEnable()
        {
            if (Renderer)Renderer.enabled = true;
        }
        
        public void Initialize(
            Spawner spawner,
            SpawnSettings.MeshInfo meshInfo,
            float mass,
            float scale,
            Vector3 velocity,
            Vector3 angularVelocity
        )
        {
            // Batch transform/rigid-body property writes to avoid repeated syncs
            var prevAutoSync = Physics.autoSyncTransforms;
            Physics.autoSyncTransforms = false;

            meshFilter.mesh = meshInfo.mesh;
            Spawner = spawner;
        
            // Calculate volume from mesh bounds and scale
            Volume = meshInfo.cachedVolume * (scale * scale * scale);
            
            Rb.mass = mass;
            transform.localScale = Vector3.one * scale;

            Rb.linearVelocity = velocity;
            Rb.angularVelocity = angularVelocity;

            UpdateMeshCollider(meshInfo);

            // Update cheap collider radius for far-field trigger
            if (cheapCollider)
            {
                // Radius equals half the largest axis of the scaled bounds
                var size = meshInfo.mesh.bounds.size;
                var radius = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * scale * 0.5f;
                cheapCollider.radius = radius;
            }

            // Store initial velocities for potential resets when reusing from the pool
            this.initialVelocity = velocity;
            this.initialAngularVelocity = angularVelocity;

            MaxHealth = Volume * healthPerUnitVolume;
            Health = MaxHealth;

            // Finalise batched property writes
            Physics.SyncTransforms();
            Physics.autoSyncTransforms = prevAutoSync;
        }

        public void ResetAsteroid()
        {
            UpdateKinematics(initialVelocity, initialAngularVelocity);
            Health = MaxHealth;
            if (Renderer)Renderer.enabled = true;
        }

        public void UpdateKinematics(Vector3 vel, Vector3 spin)
        {
            Rb.linearVelocity = vel;
            Rb.angularVelocity = spin;
        }

        private void UpdateMeshCollider(SpawnSettings.MeshInfo meshInfo)
        {
            if (!meshCollider) return;
            var targetColliderMesh = meshInfo.colliderMesh ? meshInfo.colliderMesh : meshInfo.mesh;
            if (meshCollider.sharedMesh != targetColliderMesh)
            {
                meshCollider.sharedMesh = targetColliderMesh;
            }
            meshCollider.enabled = false;
        }

        public void TakeDamage(float damage, float hitMass, Vector3 hitVelocity, Vector3 hitPoint, GameObject attacker)
        {
            var previousHealth = Health;
            Health -= damage;
            var hit = new HitData(hitMass, hitVelocity, hitPoint);
            if (Health <= 0f)
            {
                Fragger.Singleton.CreateFragments(this, hit, _=>CleanupAsteroid());
                Explode();
            }
        }

        private void Explode()
        {
            if (Renderer) Renderer.enabled = false;
            
            if (GameSettings.VfxEnabled && explosionPrefab)
            {
                var pooledVFX = explosionPrefab.GetComponent<PooledVFX>();
                if (pooledVFX)
                    SimplePool<PooledVFX>.Get(pooledVFX, transform.position, Quaternion.identity);
                else
                    Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            }
            
            if (explosionSound)
                PooledAudioSource.PlayClipAtPoint(explosionSound, transform.position, explosionVolume);
        }

        private void CleanupAsteroid()
        {
            Rb.linearVelocity = Vector3.zero;
            Rb.angularVelocity = Vector3.zero;
            Spawner.Despawn(this);
        }
        
        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(TagNames.AsteroidCullingBoundary))
                CleanupAsteroid();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.layer != LayerIds.Ship) return;
            
            var otherRb = collision.rigidbody;
            var impact = collision.GetContact(0);
            var dmg = CalcDamage(otherRb.mass, otherRb.linearVelocity, impact);
            
            var damageable = collision.gameObject.GetComponent<IDamageable>();
            damageable.TakeDamage(dmg, Mass, Rb.linearVelocity, impact.point, gameObject);
        }

        private float CalcDamage(float shipMass, Vector3 shipVel, ContactPoint impact)
        {

            var astNormalVel = Vector3.Project(Rb.linearVelocity, impact.normal);
            var shipNormalVel     = Vector3.Project(shipVel, impact.normal);

            var dmg = CollisionDamageUtility.ComputeDamage(
                Mass,astNormalVel, shipMass,shipNormalVel, energyToDamageScale);

            dmg = ApplySoftCap(dmg);
            return dmg;
        }

        private void LateUpdate()
        {
            transform.position = GamePlane.ProjectOntoPlane(transform.position) + GamePlane.Origin;

            // Enable/disable detailed collider based on distance to camera
            if (!meshCollider) return;
            if (!mainCameraTransform && Camera.main)
            {
                mainCameraTransform = Camera.main.transform;
            }

            if (!mainCameraTransform) return;
            var distSqr = (GamePlane.ProjectOntoPlane(mainCameraTransform.position) - GamePlane.ProjectOntoPlane(transform.position)).sqrMagnitude;
            var shouldEnable = distSqr < detailedColliderEnableDistance * detailedColliderEnableDistance;
            if (meshCollider.enabled != shouldEnable)
            {
                meshCollider.enabled = shouldEnable;
            }
        }

        private void OnDrawGizmos()
        {
            if (Rb != null)
            {
                Gizmos.color = Color.yellow;
                var velocity = Rb.linearVelocity;
                var start = transform.position;
                var end = start + velocity.normalized * 2f;
                Gizmos.DrawLine(start, end);
            
                var right = Quaternion.Euler(0, 0, 30) * -velocity.normalized * (2f * 0.2f);
                var left = Quaternion.Euler(0, 0, -30) * -velocity.normalized * (2f * 0.2f);
                Gizmos.DrawLine(end, end + right);
                Gizmos.DrawLine(end, end + left);
            }

            if (Application.isPlaying && MaxHealth > 0f)
            {
                var healthPercent = Mathf.Clamp01(Health / MaxHealth);
                Gizmos.color = Color.Lerp(Color.red, Color.green, healthPercent);

                // Bar dimensions relative to asteroid size
                var barLength = transform.localScale.x;
                var barSize = new Vector3(barLength * healthPercent, 0.05f * transform.localScale.x, 0.05f * transform.localScale.x);
                var barPosition = transform.position + Vector3.up * (transform.localScale.x * 0.6f);
                Gizmos.DrawCube(barPosition, barSize);
            }
        }
    }
}
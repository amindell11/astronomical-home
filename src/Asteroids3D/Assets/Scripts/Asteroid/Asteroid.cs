using Damage;
using Editor;
using Game;
using UnityEngine;
using Utils;

namespace Asteroid
{
    public class Asteroid : MonoBehaviour, IDamageable
    {
        [Header("Physical Properties")]
        [SerializeField] private float density = 1f;
    
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
            float excess = damage - softCapThreshold;
            return softCapThreshold + Mathf.Pow(excess, softCapExponent);
        }

        [Header("Health")]
        [SerializeField]
        [Tooltip("Base health per unit volume. Total health = volume * this value.")]
        private float healthPerUnitVolume = 10f;
    
        private Rigidbody rb;
        private MeshFilter meshFilter;
        private MeshCollider meshCollider;
        private SphereCollider cheapCollider;
        private Transform mainCameraTransform;


        [Header("Performance Tuning")]
        [Tooltip("Distance at which the detailed MeshCollider becomes active (units)")]
        [SerializeField] private float detailedColliderEnableDistance = 75f;

        private Vector3 initialVelocity;
        private Vector3 initialAngularVelocity;
        private float currentVolume;
        private float maxHealth;
        private float currentHealth;
        private AsteroidSpawner parentSpawner;
        private Renderer renderer;


        // Public properties for other systems to access
        public float CurrentMass => rb.mass;
        public float CurrentVolume => currentVolume;
        public float Density => density;
        public Rigidbody Rb => rb;
        public Mesh CurrentMesh => meshFilter.sharedMesh;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;


        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            meshFilter = GetComponent<MeshFilter>();
            meshCollider = GetComponent<MeshCollider>();
            cheapCollider = GetComponent<SphereCollider>();
            mainCameraTransform = Camera.main != null ? Camera.main.transform : null;
            rb.useGravity = false;
            renderer = GetComponent<Renderer>();
        }

        private void OnEnable()
        {
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }
        public void Initialize(
            AsteroidSpawnSettings.MeshInfo meshInfo,
            float mass,
            float scale,
            Vector3 velocity,
            Vector3 angularVelocity
        )
        {
            // Batch transform/rigid-body property writes to avoid repeated syncs
            bool prevAutoSync = Physics.autoSyncTransforms;
            Physics.autoSyncTransforms = false;

            this.meshFilter.mesh = meshInfo.mesh;
            parentSpawner = GetComponentInParent<AsteroidSpawner>();
        
            // Calculate volume from mesh bounds and scale
            currentVolume = meshInfo.cachedVolume * (scale * scale * scale);
        
            // Log after volume is calculated
            RLog.Asteroid($"Asteroid {gameObject.name}: Initialize | ParentSpawner: {(parentSpawner != null ? parentSpawner.name : "NULL")} | Volume: {currentVolume:F2} | Mass: {mass:F2}");
        
            this.rb.mass = mass;
            transform.localScale = Vector3.one * scale;

            rb.linearVelocity = velocity;
            rb.angularVelocity = angularVelocity;

            UpdateMeshCollider(meshInfo);

            // Update cheap collider radius for far-field trigger
            if (cheapCollider != null)
            {
                // Radius equals half the largest axis of the scaled bounds
                Vector3 size = meshInfo.mesh.bounds.size;
                float radius = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * scale * 0.5f;
                cheapCollider.radius = radius;
            }

            // Store initial velocities for potential resets when reusing from the pool
            this.initialVelocity = velocity;
            this.initialAngularVelocity = angularVelocity;

            maxHealth = currentVolume * healthPerUnitVolume;
            currentHealth = maxHealth;

            // Finalise batched property writes
            Physics.SyncTransforms();
            Physics.autoSyncTransforms = prevAutoSync;
        }

        public void ResetAsteroid()
        {
            rb.linearVelocity = initialVelocity;
            rb.angularVelocity = initialAngularVelocity;
            currentHealth = maxHealth;
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }

        private void UpdateMeshCollider(AsteroidSpawnSettings.MeshInfo meshInfo)
        {
            if (meshCollider != null)
            {
                Mesh targetColliderMesh = meshInfo.colliderMesh != null ? meshInfo.colliderMesh : meshInfo.mesh;

                // Skip reassignment if already correct to avoid unnecessary cooking
                if (meshCollider.sharedMesh != targetColliderMesh)
                {
                    meshCollider.sharedMesh = targetColliderMesh;
                }
                // Disable by default – enable when close to camera
                meshCollider.enabled = false;
            }
        }

        public void TakeDamage(float damage, float projectileMass, Vector3 projectileVelocity, Vector3 hitPoint, GameObject attacker)
        {
            float previousHealth = currentHealth;
            currentHealth -= damage;

            if (currentHealth <= 0f)
            {
                // Destroy asteroid – create fragments, VFX, and cleanup
                AsteroidFragnetics.Instance.CreateFragments(this, projectileMass, projectileVelocity, hitPoint, CleanupAsteroid);
                Explode();
            }
            else
            {
                // Optional: small hit feedback could be added here (spark VFX, sound, etc.)
            }
        }

        private void Explode()
        {
            if (renderer != null)
            {
                renderer.enabled = false;
            }

            if (GameSettings.VfxEnabled && explosionPrefab != null)
            {
                // Try to get PooledVFX component first, fallback to regular instantiate
                PooledVFX pooledVFX = explosionPrefab.GetComponent<PooledVFX>();
                if (pooledVFX != null)
                {
                    SimplePool<PooledVFX>.Get(pooledVFX, transform.position, Quaternion.identity);
                }
                else
                {
                    Instantiate(explosionPrefab, transform.position, Quaternion.identity);
                }
            }
            if (explosionSound != null)
            {
                PooledAudioSource.PlayClipAtPoint(explosionSound, transform.position, explosionVolume);
            }
        }

        private void CleanupAsteroid()
        {
            RLog.Asteroid($"Asteroid {gameObject.name}: CleanupAsteroid | ParentSpawner: {(parentSpawner != null ? parentSpawner.name : "NULL")} | Volume: {currentVolume:F2}");
        
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        
            if (parentSpawner == null)
            {
                parentSpawner = GetComponentInParent<AsteroidSpawner>();
                if (parentSpawner == null)
                {
                    parentSpawner = AsteroidSpawner.Instance;
                }
            }
        
            if (parentSpawner != null)
            {
                parentSpawner.ReleaseAsteroid(gameObject);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(TagNames.AsteroidCullingBoundary))
            {
                CleanupAsteroid();
            }
        }
        /*
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Ship"))
        {
            Debug.Log("Asteroid hit ship (trigger) – consider switching collider to non-trigger for energy-based damage.");
            // Legacy fixed damage path removed.
        }
    }
    */

        private void OnCollisionEnter(Collision collision)
        {
            // Only handle collisions with objects on the Ship layer
            if (collision.gameObject.layer != LayerIds.Ship) return;

            Rigidbody otherRb = collision.rigidbody;
            if (otherRb == null) return;

            IDamageable damageable = otherRb.GetComponent<IDamageable>();
            if (damageable == null) return;

            float shipMass = otherRb.mass;
            Vector3 shipVel = otherRb.linearVelocity;
            Vector3 impactPoint = collision.GetContact(0).point;

            // --- NEW: Use only the velocity component along the collision normal ---
            Vector3 normal = collision.GetContact(0).normal;
            Vector3 asteroidVelNormal = Vector3.Project(rb.linearVelocity, normal);
            Vector3 shipVelNormal     = Vector3.Project(shipVel, normal);

            float dmg = CollisionDamageUtility.ComputeDamage(
                CurrentMass, asteroidVelNormal,
                shipMass,     shipVelNormal,
                energyToDamageScale);

            // Apply soft cap to prevent excessive one-hit damage
            dmg = ApplySoftCap(dmg);

            damageable.TakeDamage(dmg, CurrentMass, rb.linearVelocity, impactPoint, gameObject);
        }

        private void LateUpdate()
        {
            transform.position = GamePlane.ProjectOntoPlane(transform.position) + GamePlane.Origin;

            // Enable/disable detailed collider based on distance to camera
            if (meshCollider != null)
            {
                if (mainCameraTransform == null && Camera.main != null)
                {
                    mainCameraTransform = Camera.main.transform;
                }

                if (mainCameraTransform != null)
                {
                    float distSqr = (GamePlane.ProjectOntoPlane(mainCameraTransform.position) - GamePlane.ProjectOntoPlane(transform.position)).sqrMagnitude;
                    bool shouldEnable = distSqr < detailedColliderEnableDistance * detailedColliderEnableDistance;
                    if (meshCollider.enabled != shouldEnable)
                    {
                        meshCollider.enabled = shouldEnable;
                    }
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (rb != null)
            {
                Gizmos.color = Color.yellow;
                Vector3 velocity = rb.linearVelocity;
                Vector3 start = transform.position;
                Vector3 end = start + velocity.normalized * 2f;
                Gizmos.DrawLine(start, end);
            
                Vector3 right = Quaternion.Euler(0, 0, 30) * -velocity.normalized * (2f * 0.2f);
                Vector3 left = Quaternion.Euler(0, 0, -30) * -velocity.normalized * (2f * 0.2f);
                Gizmos.DrawLine(end, end + right);
                Gizmos.DrawLine(end, end + left);
            }

            if (Application.isPlaying && maxHealth > 0f)
            {
                float healthPercent = Mathf.Clamp01(currentHealth / maxHealth);
                Gizmos.color = Color.Lerp(Color.red, Color.green, healthPercent);

                // Bar dimensions relative to asteroid size
                float barLength = transform.localScale.x;
                Vector3 barSize = new Vector3(barLength * healthPercent, 0.05f * transform.localScale.x, 0.05f * transform.localScale.x);
                Vector3 barPosition = transform.position + Vector3.up * (transform.localScale.x * 0.6f);
                Gizmos.DrawCube(barPosition, barSize);
            }
        }
    }
}
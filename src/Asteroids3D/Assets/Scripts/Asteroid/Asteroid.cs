using UnityEngine;

public class Asteroid : MonoBehaviour, IDamageable, ITargetable
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
    
    private Rigidbody rb;
    private MeshFilter meshFilter;
    private Vector3 initialVelocity;
    private Vector3 initialAngularVelocity;
    private float currentVolume;

    // Public properties for other systems to access
    public float CurrentMass => rb.mass;
    public float CurrentVolume => currentVolume;
    public float Density => density;
    public Rigidbody Rb => rb;
    public Mesh CurrentMesh => meshFilter.sharedMesh;
    public AsteroidSpawner parentSpawner;

    // ITargetable Implementation
    public Transform TargetPoint => transform;

    public LockOnIndicator Indicator { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        meshFilter = GetComponent<MeshFilter>();
        RLog.Log("Asteroid Spawner for "+gameObject+":"+parentSpawner);
        rb.useGravity = false;

        // Find indicator in children (may be inactive)
        Indicator = GetComponentInChildren<LockOnIndicator>(true);
    }

    public void Initialize(
        Mesh mesh, 
        float mass,
        float scale,
        Vector3 velocity,
        Vector3 angularVelocity
    )
    {
        this.meshFilter.mesh = mesh;
        parentSpawner = GetComponentInParent<AsteroidSpawner>();
        // Calculate volume from mesh bounds and scale
        if (mesh != null)
        {
            var bounds = mesh.bounds;
            float meshVolume = bounds.size.x * bounds.size.y * bounds.size.z;
            currentVolume = meshVolume * (scale * scale * scale); // scale^3 for volume
        }
        else
        {
            currentVolume = 1f;
        }
        
        this.rb.mass = mass;
        transform.localScale = Vector3.one * scale;

        rb.linearVelocity = velocity;
        rb.angularVelocity = angularVelocity;
        
        UpdateMeshCollider();

        // Store initial velocities for potential resets when reusing from the pool
        this.initialVelocity = velocity;
        this.initialAngularVelocity = angularVelocity;
    }

    public void ResetAsteroid()
    {
        rb.linearVelocity = initialVelocity;
        rb.angularVelocity = initialAngularVelocity;
    }
    
    /// <summary>
    /// Calculate mass from volume and density - useful for fragments
    /// </summary>
    public float CalculateMassFromVolume(float volume)
    {
        return volume * density;
    }
    
    /// <summary>
    /// Calculate mass from mesh bounds - useful during spawning
    /// </summary>
    public static float CalculateMassFromMesh(Mesh mesh, float density, float scale = 1f)
    {
        if (mesh == null) return density; // fallback to unit volume
        var bounds = mesh.bounds;
        float volume = bounds.size.x * bounds.size.y * bounds.size.z;
        float scaledVolume = volume * (scale * scale * scale);
        return scaledVolume * density;
    }

    private void UpdateMeshCollider()
    {
        MeshCollider meshCollider = GetComponent<MeshCollider>();
        if (meshCollider != null)
        {
            meshCollider.sharedMesh = meshFilter.sharedMesh;
        }
    }

    public void TakeDamage(float damage, float projectileMass, Vector3 projectileVelocity, Vector3 hitPoint, GameObject damageSource)
    {       
        AsteroidFragnetics.Instance.CreateFragments(this, projectileMass, projectileVelocity, hitPoint);
        Explode();
        CleanupAsteroid();
    }

    private void Explode()
    {
        if (explosionPrefab != null)
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
            AudioSource.PlayClipAtPoint(explosionSound, transform.position, explosionVolume);
        }
    }

    private void CleanupAsteroid()
    {
        parentSpawner.ReleaseAsteroid(gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("AsteroidCullingBoundary"))
        {
            parentSpawner.ReleaseAsteroid(gameObject);
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
        if (collision.gameObject.layer != LayerMask.NameToLayer("Ship")) return;

        Rigidbody otherRb = collision.rigidbody;
        if (otherRb == null) return;

        IDamageable damageable = otherRb.GetComponent<IDamageable>();
        if (damageable == null) return;

        float shipMass = otherRb.mass;
        Vector3 shipVel = otherRb.linearVelocity;
        Vector3 impactPoint = collision.GetContact(0).point;

        float dmg = CollisionDamageUtility.ComputeDamage(
            CurrentMass, rb.linearVelocity,
            shipMass,     shipVel,
            energyToDamageScale);

        damageable.TakeDamage(dmg, CurrentMass, rb.linearVelocity, impactPoint, gameObject);
    }

    private void LateUpdate()
    {
        transform.position = new Vector3(transform.position.x, 0, transform.position.z);
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
    }
}
using UnityEngine;

public class LaserProjectile : MonoBehaviour
{
    [Header("Laser Properties")]
    [SerializeField] private float damage = 10f;
    [SerializeField] private float maxDistance = 50f;
    [SerializeField] private GameObject hitEffect;
    [SerializeField] private float mass = 0.1f;  // Mass of the laser projectile
    [SerializeField] private AudioClip laserSound;
    [SerializeField] private float laserVolume = 0.5f;
    [SerializeField] private Vector3 referencePlaneNormal;

    private Vector3 startPosition;
    private Rigidbody rb;
    public GameObject Shooter { get; set; }

    private void OnEnable()
    {
        referencePlaneNormal = GameObject.FindGameObjectWithTag("ReferencePlane").transform.forward;
        // Reset state when retrieved from pool
        startPosition = transform.position;
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
        }
        
        // Play laser sound (only if we have the clip)
        if (laserSound != null)
        {
            AudioSource.PlayClipAtPoint(laserSound, transform.position, laserVolume);
        }
    }

    private void FixedUpdate()
    {
        // Check if laser has traveled too far
        float distanceTraveled = Vector3.Distance(startPosition, transform.position);
        transform.position = Vector3.ProjectOnPlane(transform.position, referencePlaneNormal);
        if (distanceTraveled > maxDistance)
        {
            ReturnToPool();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Ignore collision with the object that fired the laser
        if (Shooter != null && other.transform.root.gameObject == Shooter)
        {
            return;
        }

        #if UNITY_EDITOR
        Debug.Log($"OnTriggerEnter: {other.gameObject.name}");
        #endif
        
        // Check if the object can take damage
        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage, rb.mass, rb.velocity, transform.position);
            
            // Spawn hit effect if we have one
            if (hitEffect != null)
            {
                // Try to get PooledVFX component first, fallback to regular instantiate
                PooledVFX pooledVFX = hitEffect.GetComponent<PooledVFX>();
                if (pooledVFX != null)
                {
                    SimplePool<PooledVFX>.Get(pooledVFX, transform.position, Quaternion.identity);
                }
                else
                {
                    Instantiate(hitEffect, transform.position, Quaternion.identity);
                }
            }
            
            ReturnToPool();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Boundary"))
        {
            ReturnToPool();
        }
    }
    
    /// <summary>
    /// Return this laser to the pool instead of destroying it
    /// </summary>
    private void ReturnToPool()
    {
        // Reset shooter reference
        Shooter = null;
        
        // Stop any physics
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        // Return to pool
        SimplePool<LaserProjectile>.Release(this);
    }
} 
using UnityEngine;

/// <summary>
/// Base behaviour for all projectile types – handles lifespan, damage application and hit VFX.
/// Concrete subclasses must implement ReturnToPool() to return themselves to the correct SimplePool stack.
/// </summary>
public abstract class ProjectileBase : MonoBehaviour
{
    [Header("Base Projectile Settings")]
    [SerializeField] protected float damage        = 10f;
    [SerializeField] protected float maxDistance   = 50f;
    [SerializeField] protected GameObject hitEffect;
    [SerializeField] protected float mass          = 0.1f;

    protected Rigidbody rb;
    protected Vector3   startPosition;
    protected Vector3   referencePlaneNormal;

    /// <summary>The GameObject that spawned/fired this projectile.  Used to ignore self-hits.</summary>
    public GameObject Shooter { get; set; }

    /* ───────────────────────── Unity callbacks ───────────────────────── */
    protected virtual void OnEnable()
    {
        // Cache plane normal (assumes there is exactly one tagged object)
        var refPlane = GameObject.FindGameObjectWithTag("ReferencePlane");
        if (refPlane) referencePlaneNormal = refPlane.transform.forward;

        startPosition = transform.position;
        rb            = GetComponent<Rigidbody>();

        if (rb)
        {
            rb.useGravity = false;
            rb.mass       = mass;
        }
    }

    protected virtual void FixedUpdate()
    {
        // Keep projectile constrained to the game plane (top-down gameplay)
        if (referencePlaneNormal != Vector3.zero)
        {
            transform.position = Vector3.ProjectOnPlane(transform.position, referencePlaneNormal);
        }

        // Lifetime based on travel distance
        if (Vector3.Distance(startPosition, transform.position) > maxDistance)
        {
            ReturnToPool();
        }
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        // Ignore our own ship
        if (Shooter && other.transform.root.gameObject == Shooter) return;

#if UNITY_EDITOR
        Debug.Log($"Projectile hit: {other.gameObject.name}");
#endif
        // Apply damage if possible
        IDamageable dmg = other.GetComponentInParent<IDamageable>();
        if (dmg != null)
        {
            Vector3 impactVelocity = rb ? rb.linearVelocity : Vector3.zero;
            dmg.TakeDamage(damage, mass, impactVelocity, transform.position);
            SpawnHitVFX();
            ReturnToPool();
        }
    }

    protected virtual void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Boundary")) ReturnToPool();
    }

    /* ───────────────────────── helpers ───────────────────────── */
    void SpawnHitVFX()
    {
        if (!hitEffect) return;

        var pooled = hitEffect.GetComponent<PooledVFX>();
        if (pooled)
        {
            SimplePool<PooledVFX>.Get(pooled, transform.position, Quaternion.identity);
        }
        else
        {
            Instantiate(hitEffect, transform.position, Quaternion.identity);
        }
    }

    /// <summary>Return this projectile instance to its pool. Implemented by subclass.</summary>
    protected abstract void ReturnToPool();
}
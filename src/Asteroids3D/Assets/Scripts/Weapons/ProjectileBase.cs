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

    // Cache ReferencePlane info once to avoid per-projectile lookups (Optimization #3)
    private static Transform s_cachedPlaneTransform;
    private static Vector3   s_cachedPlaneNormal;

    /// <summary>
    /// The entity that fired this projectile. Used for velocity inheritance,
    /// self-hit checks, and attributing damage.
    /// </summary>
    public IShooter Shooter { get; set; }

    public float Damage => damage;
    
    /* ───────────────────────── Unity callbacks ───────────────────────── */
    protected virtual void OnEnable()
    {   
        // Cache plane normal (assumes there is exactly one tagged object)
        if (s_cachedPlaneTransform == null)
        {
            var refPlane = GameObject.FindGameObjectWithTag("ReferencePlane");
            if (refPlane)
            {
                s_cachedPlaneTransform = refPlane.transform;
                s_cachedPlaneNormal    = s_cachedPlaneTransform.forward;
            }
        }
        referencePlaneNormal = s_cachedPlaneNormal;

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
    protected virtual void OnHit(IDamageable other){
        RLog.Weapon($"applying {damage} damage to {other.gameObject.name}");
        
        Vector3 impactVelocity = rb ? rb.linearVelocity : Vector3.zero;
        other.TakeDamage(damage, mass, impactVelocity, transform.position, Shooter?.gameObject);
        SpawnHitVFX();
        ReturnToPool();
    }
    protected virtual void OnTriggerEnter(Collider other)
    {
        // Resolve the IDamageable (if any) that was hit
        IDamageable dmg = other.GetComponentInParent<IDamageable>();

        // Early-out if we didn't hit something that can take damage
        if (dmg == null) return;

        // Ignore self-hits by checking if the other collider belongs to the shooter.
        // This is more robust than a direct GameObject comparison because colliders
        // may be on child objects.
        if (Shooter != null && other.GetComponentInParent<IShooter>() == Shooter) return;

        RLog.Weapon($"Projectile hit: {other.gameObject.name}");
        OnHit(dmg);
    }

    protected virtual void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Boundary")) ReturnToPool();
    }

    /* ───────────────────────── helpers ───────────────────────── */
    void SpawnHitVFX()
    {
        if (!hitEffect || !GameSettings.VfxEnabled) return;

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
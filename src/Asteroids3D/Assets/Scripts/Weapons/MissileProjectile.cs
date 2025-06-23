using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Homing missile projectile that steers towards a target and explodes with AoE damage on impact.
/// </summary>
public class MissileProjectile : ProjectileBase, IDamageable
{
    [Header("Missile Homing")]
    [SerializeField] private float homingSpeed      = 15f;
    [SerializeField] private float homingTurnRate   = 90f; // degrees per second

    [Header("Explosion")]    
    [SerializeField] private float   explosionRadius  = 3f;
    [SerializeField] private float splashDamage = 5f;
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private LayerMask damageLayerMask = -1; // Default to all layers, configured in inspector

    [Header("Motion")]
    [Tooltip("Initial launch speed in units/sec.")]
    [SerializeField] private float initialSpeed = 15f;

    [Tooltip("Continuous forward acceleration in units/sec^2 applied every FixedUpdate.")]
    [SerializeField] private float acceleration  = 40f;

    [Tooltip("Force multiplier applied to the target on impact.")]
    [SerializeField] private float impactForceMultiplier = 10f;

    // Assigned by launcher
    private Transform target;
    public void SetTarget(Transform tgt) => target = tgt;

    // Pre-allocated buffer for explosion overlap queries (Optimization #3)
    private static readonly Collider[] explosionHitBuffer = new Collider[64];

    private void Awake()
    {
        // Initialize damage layer mask to target ships and asteroids
        if (damageLayerMask == -1) // If not configured in inspector, set default
        {
            damageLayerMask = (1 << LayerMask.NameToLayer("Ship")) | (1 << LayerMask.NameToLayer("Asteroid"));
        }
    }

    /* ───────────────────────── Unity callbacks ───────────────────────── */
    protected override void OnEnable()
    {
        RLog.Log($"MissileProjectile OnEnable at position: {transform.position}, rotation: {transform.rotation}");
        base.OnEnable();
        // Initial straight velocity
        if (rb) 
        {
            rb.linearVelocity = transform.up * initialSpeed;
            RLog.Log($"Missile initial velocity set to: {rb.linearVelocity}, speed: {initialSpeed}");
            rb.maxLinearVelocity = homingSpeed;
        }
        else
        {
            RLog.LogError("MissileProjectile: No Rigidbody found!");
        }
    }
    

    protected override void FixedUpdate()
    {
        // Debug before base call
        float distanceTraveled = Vector3.Distance(startPosition, transform.position);
        if (distanceTraveled > maxDistance * 0.9f) // Warn when getting close to limit
        {
            RLog.LogWarning($"Missile approaching max distance: {distanceTraveled}/{maxDistance}");
        }
        
        base.FixedUpdate();

        if (rb)
        {
            // 1. Compute the desired heading (toward target if any, otherwise keep current).
            Vector3 desiredDir = target ? (target.position - transform.position).normalized : transform.up;

            // 2. Determine signed angle (degrees) from current heading to desired direction in game-plane.
            float signedAngle = Vector3.SignedAngle(transform.up, desiredDir, GamePlane.Normal);

            // 3. Clamp the turn by homingTurnRate per physics-step and rotate.
            float maxTurnThisStep = homingTurnRate * Time.fixedDeltaTime; // °/frame
            rotationCorrectionDeg = Mathf.Clamp(signedAngle, -maxTurnThisStep, maxTurnThisStep);
            transform.rotation = Quaternion.AngleAxis(rotationCorrectionDeg, GamePlane.Normal) * transform.rotation;

            // 3. Apply engine thrust along the missile's current forward (transform.up).
            rb.AddForce(transform.up * acceleration, ForceMode.Acceleration);
        }
    }
    // Debug – the angle actually applied this physics-step (degrees)
    float rotationCorrectionDeg = 0f;

    protected override void OnHit(IDamageable other)
    {
        base.OnHit(other);
        
     /*   // Apply impact force to the other object's rigidbody
        if (rb && other?.gameObject)
        {
            Rigidbody otherRb = other.gameObject.GetComponent<Rigidbody>();
            if (otherRb)
            {
                Vector3 forceDirection = rb.linearVelocity.normalized;
                float forceMagnitude = rb.linearVelocity.magnitude * impactForceMultiplier;
                otherRb.AddForce(forceDirection * forceMagnitude, ForceMode.Impulse);
                RLog.Log($"Applied impact force to {other.gameObject.name}: {forceDirection * forceMagnitude}");
            }
        }*/
        
        Explode(other);
    }

    public void TakeDamage(float damage, float projectileMass, Vector3 projectileVelocity, Vector3 hitPoint, GameObject damageSource){
        Explode(null);
    }

    /* ───────────────────────── internal ───────────────────────── */
    void Explode(IDamageable other)
    {
        // Spawn explosion VFX
        if (explosionPrefab)
        {
            var pooled = explosionPrefab.GetComponent<PooledVFX>();
            if (pooled)
                SimplePool<PooledVFX>.Get(pooled, transform.position, Quaternion.identity);
            else
                Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        }

        // AoE damage using non-allocating overlap sphere (Optimization #3)
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, explosionRadius, explosionHitBuffer, damageLayerMask);
        for (int i = 0; i < hitCount; i++)
        {
            var hit = explosionHitBuffer[i];
            if (Shooter && hit.transform.root.gameObject == Shooter) continue;
            if (other!=null && hit.transform.gameObject == other.gameObject) continue;
            
            RLog.Log($"Splash Hit {hit.name}");
            IDamageable dmg = hit.GetComponentInParent<IDamageable>();
            if (dmg != null)
            {
                dmg.TakeDamage(splashDamage, mass, rb ? rb.linearVelocity : Vector3.zero, hit.ClosestPoint(transform.position), gameObject);
            }
        }

        ReturnToPool();
    }

    /* ───────────────────────── pooling ───────────────────────── */
    protected override void ReturnToPool()
    {
        RLog.Log($"MissileProjectile returning to pool at position: {transform.position}");
        target = null;
        Shooter = null;

        if (rb)
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        SimplePool<MissileProjectile>.Release(this);
    }

#if UNITY_EDITOR
    /* ───────────────────────── Debug Gizmos ───────────────────────── */
    void OnDrawGizmos()
    {   
        // Draw game-plane normal (for orientation reference)
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, GamePlane.Normal * 2f);

        // Draw missile position
        Gizmos.color = target ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        
        // Draw velocity vector
        if (rb && rb.linearVelocity.magnitude > 0.1f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, rb.linearVelocity.normalized * 2f);
        }
        
        // Draw line to target
        if (target)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, target.position);
        }
        
        // Draw explosion radius
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
        
        // Draw travel distance
        if (Application.isPlaying)
        {
            UnityEditor.Handles.color = Color.white;
            float traveled = Vector3.Distance(startPosition, transform.position);
            UnityEditor.Handles.Label(transform.position + Vector3.up, $"Missile\nDist: {traveled:F1}/{maxDistance:F1}");
        }

        // Draw rotation correction arc (only when a target exists and we applied a turn)
        if (target && Mathf.Abs(rotationCorrectionDeg) > 0.1f)
        {
            UnityEditor.Handles.color = Color.magenta;
            UnityEditor.Handles.DrawWireArc(
                transform.position,               // centre
                GamePlane.Normal,                  // plane normal
                transform.up,                      // start direction
                rotationCorrectionDeg,             // sweep angle (deg)
                2f);                               // radius
        }
    }
#endif
} 
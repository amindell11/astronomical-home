using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Homing missile projectile that steers towards a target and explodes with AoE damage on impact.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MissileProjectile : ProjectileBase, IDamageable
{
    [Header("Missile Homing")]
    [SerializeField] private float homingSpeed      = 15f;
    [SerializeField] private float homingTurnRate   = 90f; // degrees per second

    [Header("Explosion")]    
    [SerializeField] private float   explosionRadius  = 3f;
    [SerializeField] private float splashDamage = 5f;
    [SerializeField] private PooledVFX explosionPrefab; // pooled VFX prefab
    [SerializeField] private LayerMask damageLayerMask = -1; // Default to all layers, configured in inspector

    [Header("Motion")]
    [Tooltip("Initial launch speed in units/sec.")]
    [SerializeField] private float initialSpeed = 15f;

    [Tooltip("Continuous forward acceleration in units/sec^2 applied every FixedUpdate.")]
    [SerializeField] private float acceleration  = 40f;

    [Tooltip("Force multiplier applied to the target on impact.")]
    [SerializeField] private float impactForceMultiplier = 10f;

    [Header("Audio")]
    [SerializeField] private AudioClip launchClip;
    [SerializeField] private AudioClip engineLoopClip;
    [SerializeField] private AudioClip detonationClip;
    [Tooltip("Delay before the engine loop starts playing (seconds)")]
    [SerializeField] private float engineStartDelay = 0.15f;
    [Tooltip("Duration for engine loop volume to fade in (seconds)")]
    [SerializeField] private float engineFadeInTime = 0.25f;
    [Range(0f,1f)] [SerializeField] private float launchVolume     = 1f;
    [Range(0f,1f)] [SerializeField] private float engineVolume     = 0.7f;
    [Range(0f,1f)] [SerializeField] private float detonationVolume = 1f;

    private AudioSource audioSrc;
    private Coroutine engineCo;

    // Assigned by launcher
    private Transform target;
    public void SetTarget(Transform tgt) => target = tgt;

    // Pre-allocated buffer for explosion overlap queries (Optimization #3)
    // private static readonly Collider[] explosionHitBuffer = new Collider[64];

    private void Awake()
    {
        // Initialize damage layer mask to target ships and asteroids
        if (damageLayerMask == -1) // If not configured in inspector, set default
        {
            damageLayerMask = LayerIds.Mask(LayerIds.Ship, LayerIds.Asteroid);
        }
        audioSrc = GetComponent<AudioSource>();
        if (audioSrc)
        {
            audioSrc.playOnAwake  = false;
            audioSrc.loop         = false;
            audioSrc.spatialBlend = 1f;
        }
    }

    /* ───────────────────────── Unity callbacks ───────────────────────── */
    protected override void OnEnable()
    {
        RLog.Weapon($"MissileProjectile OnEnable at position: {transform.position}, rotation: {transform.rotation}");
        base.OnEnable();
        // Note: Shooter-dependent initialization moved to Initialize() method
    }

    /// <summary>
    /// Initialize the missile with its shooter and set initial velocity.
    /// This must be called after OnEnable.
    /// </summary>
    public override void Initialize(IShooter shooter)
    {
        base.Initialize(shooter);
        
        if (rb) 
        {
            RLog.Weapon($"Shooter: "+Shooter);
            Vector3 shooterVelocity = (Shooter != null) ? Shooter.Velocity : Vector3.zero;
            rb.linearVelocity = transform.up * initialSpeed + shooterVelocity;
            RLog.Weapon($"Missile initial velocity set to: {rb.linearVelocity}, speed: {initialSpeed}");
            rb.maxLinearVelocity = homingSpeed;
        }
        else
        {
            RLog.WeaponError("MissileProjectile: No Rigidbody found!");
        }
        // ───── Audio: play launch one-shot and schedule engine loop ─────
        if (audioSrc && launchClip)
        {
            audioSrc.Stop();
            audioSrc.PlayOneShot(launchClip, launchVolume);
        }
        if (engineCo != null) StopCoroutine(engineCo);
        engineCo = StartCoroutine(StartEngineLoop());
    }

    IEnumerator StartEngineLoop()
    {
        if (engineLoopClip == null || audioSrc == null) yield break;
        yield return new WaitForSeconds(engineStartDelay);
        audioSrc.clip   = engineLoopClip;
        audioSrc.volume = 0f;
        audioSrc.loop   = true;
        audioSrc.Play();

        // Fade volume up to target over engineFadeInTime seconds
        if (engineFadeInTime > 0f)
        {
            for (float t = 0f; t < engineFadeInTime; t += Time.deltaTime)
            {
                float k = t / engineFadeInTime;
                audioSrc.volume = Mathf.Lerp(0f, engineVolume, k);
                yield return null;
            }
        }
        audioSrc.volume = engineVolume;
    }
    

    protected override void FixedUpdate()
    {
        // Debug before base call
        float distanceTraveled = Vector3.Distance(startPosition, transform.position);
        if (distanceTraveled > maxDistance * 0.9f) // Warn when getting close to limit
        {
            RLog.WeaponWarning($"Missile approaching max distance: {distanceTraveled}/{maxDistance}");
        }
        
        base.FixedUpdate();

        if (rb)
        {
            // 1. Compute the desired heading (toward target if any, otherwise keep current).
            Vector3 desiredDir = transform.up; // Default to current heading
            
            if (target != null)
            {
                Vector3 toTarget = target.position - transform.position;
                if (toTarget.sqrMagnitude > 0.01f) // Avoid division by zero
                {
                    desiredDir = toTarget.normalized;
                    
                    // Debug logging
                    if (Time.frameCount % 30 == 0) // Log every 30 frames
                    {
                        RLog.Weapon($"Missile homing: pos={transform.position}, target={target.position}, toTarget={toTarget}, desiredDir={desiredDir}");
                    }
                }
            }

            // 2. Get the proper up vector for rotation (should be Y axis for top-down)
            Vector3 rotationAxis = GamePlane.Normal; // Use world up for top-down games
            
            // If GamePlane is initialized and has a valid normal, use it
            if (GamePlane.Plane != null)
            {
                rotationAxis = GamePlane.Normal;
                // Ensure the normal is pointing up (not down)
                if (Vector3.Dot(rotationAxis, GamePlane.Normal) < 0)
                {
                    rotationAxis = -rotationAxis;
                }
            }

            // 3. Calculate rotation needed
            float signedAngle = Vector3.SignedAngle(transform.up, desiredDir, rotationAxis);

            // 4. Clamp the turn by homingTurnRate per physics-step and rotate.
            float maxTurnThisStep = homingTurnRate * Time.fixedDeltaTime; // °/frame
            rotationCorrectionDeg = Mathf.Clamp(signedAngle, -maxTurnThisStep, maxTurnThisStep);
            
            // Apply rotation if significant
            if (Mathf.Abs(rotationCorrectionDeg) > 0.01f)
            {
                transform.rotation = Quaternion.AngleAxis(rotationCorrectionDeg, rotationAxis) * transform.rotation;
            }

            // 5. Compute the desired velocity along the missile's forward direction.
            Vector3 desiredVelocity = transform.up * homingSpeed;

            // 6. Smoothly rotate the current velocity toward the desired velocity, limiting
            //    both the angular change (maxTurnRad) and the linear acceleration (maxAccelThisStep)
            //    in this physics step. This prevents the missile from overshooting and spiralling
            //    around the target.
            float maxTurnRad       = homingTurnRate * Mathf.Deg2Rad * Time.fixedDeltaTime;   // radians/frame
            float maxAccelThisStep = acceleration   * Time.fixedDeltaTime;                  // units/sec per frame

            rb.linearVelocity = Vector3.RotateTowards(
                                    rb.linearVelocity,
                                    desiredVelocity,
                                    maxTurnRad,
                                    maxAccelThisStep);

            // 7. Ensure we never exceed the maximum homing speed.
            if (rb.linearVelocity.magnitude > homingSpeed)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * homingSpeed;
            }
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
                RLog.Weapon($"Applied impact force to {other.gameObject.name}: {forceDirection * forceMagnitude}");
            }
        }
        */
        Explode(other);
    }

    public void TakeDamage(float damage, float projectileMass, Vector3 projectileVelocity, Vector3 hitPoint, GameObject attacker){
        Explode(null);
    }

    /* ───────────────────────── internal ───────────────────────── */
    void Explode(IDamageable other)
    {
        // Spawn explosion VFX
        if (GameSettings.VfxEnabled && explosionPrefab)
        {
            // Always use pooled VFX to avoid instantiation overhead
            SimplePool<PooledVFX>.Get(explosionPrefab, transform.position, Quaternion.identity);
        }

        // AoE damage using non-allocating overlap sphere (Optimization #3)
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, explosionRadius, PhysicsBuffers.GetColliderBuffer(64), damageLayerMask);
        for (int i = 0; i < hitCount; i++)
        {
            var hit = PhysicsBuffers.GetColliderBuffer(64)[i];

            IDamageable dmg = hit.GetComponentInParent<IDamageable>();

            // Skip if the collider doesn't belong to something damageable
            if (dmg == null) continue;

            // Ignore the shooter itself
            if (Shooter != null && dmg.gameObject == Shooter.gameObject) continue;

            // Ignore the primary impact target (already handled in OnHit)
            if (other != null && dmg == other) continue;

            RLog.Weapon($"Splash Hit {hit.name}");
            dmg.TakeDamage(splashDamage, mass, rb ? rb.linearVelocity : Vector3.zero, hit.ClosestPoint(transform.position), Shooter?.gameObject);
        }

        ReturnToPool();
        // Audio – detonation one-shot at point of impact before returning to pool
        if (detonationClip)
            PooledAudioSource.PlayClipAtPoint(detonationClip, transform.position, detonationVolume);
        if (audioSrc) audioSrc.Stop();
    }

    /* ───────────────────────── pooling ───────────────────────── */
    protected override void ReturnToPool()
    {
        RLog.Weapon($"MissileProjectile returning to pool at position: {transform.position}");
        target = null;
        Shooter = null;

        if (rb)
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        SimplePool<MissileProjectile>.Release(this);
        if (engineCo != null) { StopCoroutine(engineCo); engineCo = null; }
        if (audioSrc) audioSrc.Stop();
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
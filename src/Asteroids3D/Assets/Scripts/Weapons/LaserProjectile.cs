using UnityEngine;

public class LaserProjectile : ProjectileBase
{
    [Header("Laser Properties")]
    [SerializeField] private float laserSpeed = 20f;

    [Header("Fade Settings")]
    [Tooltip("Alpha fade curve based on distance travelled as a percentage of maxDistance. 0 = fully opaque, 1 = fully transparent")]
    [SerializeField] private AnimationCurve fadeCurve = new AnimationCurve(
        new Keyframe(0f, 0f),      // No fade initially
        new Keyframe(0.7f, 0f),    // Remain fully visible until ~70% of max distance
        new Keyframe(1f, 1f)       // Fully transparent at max distance
    );

    private Renderer[] renderers;
    private Color[]    originalColors;

    public float LaserSpeed => laserSpeed;
    
    /* ───────────────────────── Unity callbacks ───────────────────────── */

    protected override void OnEnable()
    {
        base.OnEnable();

        // Cache renderers & their starting colours so we can fade without instantiating new materials per projectile
        renderers = GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            originalColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                originalColors[i] = renderers[i].material.color;
            }
        }
    }

    /// <summary>
    /// Initialize the laser with its shooter and set velocity with relative motion.
    /// </summary>
    public override void Initialize(IShooter shooter)
    {
        base.Initialize(shooter);
        
        if (rb) 
        {
            // Add shooter's velocity for relative motion (like missiles do)
            Vector3 shooterVelocity = (Shooter != null) ? Shooter.Velocity : Vector3.zero;
            Vector3 forward      = transform.up;
            Vector3 inheritAlong = Vector3.Project(shooterVelocity, forward);
            rb.linearVelocity    = forward * laserSpeed + inheritAlong;
            RLog.Weapon($"Laser velocity set to: {rb.linearVelocity}, base speed: {laserSpeed}");
        }
    }

    protected override void FixedUpdate()
    {
        if (renderers != null && renderers.Length > 0)
        {
            // Calculate how far we've travelled as a fraction of the allowed distance
            float travelled   = Vector3.Distance(startPosition, transform.position);
            float normalized  = Mathf.Clamp01(travelled / maxDistance);

            // fadeCurve returns 0 (opaque) -> 1 (transparent); invert so 1 = opaque, 0 = transparent
            float alphaFactor = 1f - fadeCurve.Evaluate(normalized);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i]) continue;
                Color c = (originalColors != null && i < originalColors.Length) ? originalColors[i] : renderers[i].material.color;
                c.a *= alphaFactor;
                renderers[i].material.color = c;
            }
        }

        // Now execute base logic (which may call ReturnToPool and reset colours)
        base.FixedUpdate();
    }

    /* ───────────────────────── pooling ───────────────────────── */
    protected override void ReturnToPool()
    {
        // Reset shooter reference and physics state
        Shooter = null;
        if (rb)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Restore original colours so the projectile is fully visible when reused from pool
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i]) continue;
                if (originalColors != null && i < originalColors.Length)
                {
                    renderers[i].material.color = originalColors[i];
                }
            }
        }
 
        SimplePool<LaserProjectile>.Release(this);
    }
} 
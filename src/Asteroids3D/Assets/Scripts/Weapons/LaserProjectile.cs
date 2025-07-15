using UnityEngine;

public class LaserProjectile : ProjectileBase
{
    [Header("Laser Properties")]
    [SerializeField] private float laserSpeed = 20f;

    public float LaserSpeed => laserSpeed;
    
    /* ───────────────────────── Unity callbacks ───────────────────────── */
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
            rb.linearVelocity = transform.up * laserSpeed + shooterVelocity;
            RLog.Weapon($"Laser velocity set to: {rb.linearVelocity}, base speed: {laserSpeed}");
        }
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
        SimplePool<LaserProjectile>.Release(this);
    }
} 
using UnityEngine;

public class LaserProjectile : ProjectileBase
{
    [Header("Laser Properties")]
    [SerializeField] private float laserSpeed = 20f;
    [SerializeField] private AudioClip laserSound;
    [SerializeField] private float laserVolume = 0.5f;

    /* ───────────────────────── Unity callbacks ───────────────────────── */
    protected override void OnEnable()
    {
        base.OnEnable();

        if (rb) rb.linearVelocity = transform.up * laserSpeed;

        if (laserSound)
        {
            AudioSource.PlayClipAtPoint(laserSound, transform.position, laserVolume);
        }
    }

    /* ───────────────────────── pooling ───────────────────────── */
    protected override void ReturnToPool()
    {
        // Reset shooter reference and physics state
        Shooter = null;
        if (rb)
        {
            rb.linearVelocity        = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        SimplePool<LaserProjectile>.Release(this);
    }
    protected override void OnHit(IDamageable other)
    {
        Debug.Log("Laser hit "+other+" Shooter="+Shooter);
    }
} 
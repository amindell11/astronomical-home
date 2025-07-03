using UnityEngine;
// One abstract, non-generic root that Unity can serialize
public abstract class WeaponComponent : MonoBehaviour, IWeapon
{
    public abstract ProjectileBase Fire();
    public abstract bool CanFire();
}
/// <summary>
/// Generic weapon/launcher base – spawns pooled projectiles of type <typeparamref name="TProj"/>.
/// </summary>
/// <typeparam name="TProj">Projectile component the launcher fires.</typeparam>
public abstract class LauncherBase<TProj> : WeaponComponent where TProj : ProjectileBase
{
    [Header("Launcher Settings")]
    [SerializeField] protected TProj     projectilePrefab;
    [SerializeField] public    Transform firePoint;   // exposed for AI scripts
    [SerializeField] protected float     fireRate = 0.2f;

    [Header("Audio")]
    [SerializeField] private AudioClip fireSound;
    [SerializeField, Range(0f,1f)] private float fireVolume = 0.5f;

    protected float nextFireTime;
    protected IShooter shooter;

    protected virtual void Awake()
    {
        shooter = GetComponentInParent<IShooter>();
        RLog.Weapon($"BaseWeapon Shooter: {shooter}");
        if (!firePoint) firePoint = transform;
    }

    // `CanFire` now checks the fire-rate cooldown. Subclasses should call base.CanFire().
    public override bool CanFire()
    {
        return Time.time >= nextFireTime;
    }

    /// <summary>Attempts to fire a projectile if the fire-rate cooldown has elapsed.</summary>
    /// <returns>The fired projectile instance, or null if a shot was not fired.</returns>
    public override ProjectileBase Fire()
    {
        if (!CanFire()) return null;
        if (!projectilePrefab) return null;

        nextFireTime = Time.time + fireRate;

        // Grab instance from pool and stamp shooter reference
        TProj proj = SimplePool<TProj>.Get(projectilePrefab, firePoint.position, firePoint.rotation);

        // Initialize projectile with shooter (all projectiles now have Initialize method)
        proj.Initialize(shooter);

        // Play firing audio (if any)
        PlayFireSound();

        return proj;
    }

    /// <summary>
    /// Plays the firing sound using the shooter's AudioSource if available,
    /// otherwise falls back to a 3D clip at the fire point.
    /// </summary>
    protected virtual void PlayFireSound()
    {
        if (!fireSound) return;

        AudioSource src = null;
        if (shooter != null)
        {
            // Try to find an AudioSource on the shooter or its children
            src = shooter.gameObject.GetComponentInChildren<AudioSource>();
        }

        if (src)
        {
            src.PlayOneShot(fireSound, fireVolume);
        }
        else
        {
            PooledAudioSource.PlayClipAtPoint(fireSound, firePoint.position, fireVolume);
        }
    }
} 
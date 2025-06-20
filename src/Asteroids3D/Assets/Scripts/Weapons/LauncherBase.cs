using UnityEngine;
// One abstract, non-generic root that Unity can serialize
public abstract class WeaponComponent : MonoBehaviour, IWeapon
{
    public abstract void Fire();
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

    float nextFireTime;

    /// <summary>Attempts to fire a projectile if the fire-rate cooldown has elapsed.</summary>
    public override void Fire()
    {
        if (Time.time < nextFireTime) return;
        if (!projectilePrefab)       return;
    
        if (!firePoint) firePoint = transform;

        nextFireTime = Time.time + fireRate;

        // Grab instance from pool and stamp shooter reference
        TProj proj = SimplePool<TProj>.Get(projectilePrefab, firePoint.position, firePoint.rotation);
        proj.Shooter = transform.root.gameObject;
    }
} 
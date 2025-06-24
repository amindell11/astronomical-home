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

    protected float nextFireTime;

    /// <summary>Attempts to fire a projectile if the fire-rate cooldown has elapsed.</summary>
    public override void Fire()
    {
        if (Time.time < nextFireTime) return;
        if (!projectilePrefab)       return;
    
        if (!firePoint) firePoint = transform;

        nextFireTime = Time.time + fireRate;

        // Grab instance from pool and stamp shooter reference
        TProj proj = SimplePool<TProj>.Get(projectilePrefab, firePoint.position, firePoint.rotation);

        // Capture the IDamageable belonging to the shooter (if any). This allows projectiles to reliably ignore
        // self-collisions even when the shooter is nested under additional parents (e.g., an "Arena" GameObject).
        IDamageable shooterDmg = GetComponentInParent<IDamageable>();
        RLog.Log($"ShooterDmg: {shooterDmg}");

        // Fall back to root GameObject reference if no IDamageable could be found (e.g., scenery weapons).
        proj.Shooter            = shooterDmg != null ? shooterDmg.gameObject : transform.root.gameObject;
        proj.ShooterDamageable  = shooterDmg;
    }
} 
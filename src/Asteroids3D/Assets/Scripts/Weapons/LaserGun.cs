using UnityEngine;

public class LaserGun : MonoBehaviour
{
    [Header("Laser Settings")]
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] public Transform firePoint;
    [SerializeField] private float fireRate = 0.2f;

    private float nextFireTime;

    private void Start()
    {

    }

    public void Fire()
    {
        if (Time.time < nextFireTime)
        {
            return;
        }
        
        if(firePoint == null)
        {
            firePoint = transform;
        }
        if (projectilePrefab == null) return;
        
        nextFireTime = Time.time + fireRate;

        // Get laser from pool instead of instantiating
        Projectile laser = SimplePool<Projectile>.Get(projectilePrefab, firePoint.position, firePoint.rotation);

        // Assign the shooter to the laser so it can ignore collisions with itself
        laser.Shooter = transform.root.gameObject;
    }
} 
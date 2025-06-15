using UnityEngine;

public class LaserGun : MonoBehaviour
{
    [Header("Laser Settings")]
    [SerializeField] private LaserProjectile laserPrefab;
    [SerializeField] public Transform firePoint;
    [SerializeField] private float laserSpeed = 20f;
    [SerializeField] private float fireRate = 0.2f;
    [SerializeField] private AudioClip shootSound;

    private float nextFireTime;
    private AudioSource audioSource;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
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
        if (laserPrefab == null) return;
        
        nextFireTime = Time.time + fireRate;

        // Get laser from pool instead of instantiating
        LaserProjectile laser = SimplePool<LaserProjectile>.Get(laserPrefab, firePoint.position, firePoint.rotation);

        // Assign the shooter to the laser so it can ignore collisions with itself
        laser.Shooter = transform.root.gameObject;
        
        // Set up the laser's velocity
        Rigidbody rb = laser.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = firePoint.up * laserSpeed;
        }

        // Play sound effect
        if (shootSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(shootSound);
        }

        // No need to destroy - laser handles its own lifecycle now
    }
} 
using UnityEngine;

public class LaserGun : MonoBehaviour
{
    [Header("Laser Settings")]
    [SerializeField] private GameObject laserPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float laserSpeed = 20f;
    [SerializeField] private float fireRate = 0.2f;
    [SerializeField] private float laserLifetime = 2f;
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

    private void Update()
    {
        // Check for fire input (space bar or left mouse button)
        if ((Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)) && Time.time >= nextFireTime)
        {
            FireLaser();
            nextFireTime = Time.time + fireRate;
        }
    }

    private void FireLaser()
    {
        if(firePoint == null)
        {
            firePoint = transform;
        }
        if (laserPrefab == null || firePoint == null) return;

        // Instantiate the laser at the fire point
        GameObject laser = Instantiate(laserPrefab, firePoint.position, firePoint.rotation);
        
        // Set up the laser's velocity
        Rigidbody2D rb = laser.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = firePoint.up * laserSpeed;
        }

        // Play sound effect
        if (shootSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(shootSound);
        }

        // Destroy the laser after its lifetime
        Destroy(laser, laserLifetime);
    }
} 
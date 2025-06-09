using UnityEngine;

public class LaserProjectile : MonoBehaviour
{
    [Header("Laser Properties")]
    [SerializeField] private float damage = 10f;
    [SerializeField] private float maxDistance = 50f;
    [SerializeField] private GameObject hitEffect;
    [SerializeField] private float mass = 0.1f;  // Mass of the laser projectile
    [SerializeField] private AudioClip laserSound;
    [SerializeField] private float laserVolume = 0.5f;

    private Vector3 startPosition;
    private Rigidbody rb;

    private void Start()
    {
        startPosition = transform.position;
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;  
        AudioSource.PlayClipAtPoint(laserSound, transform.position, laserVolume);
    }

    private void Update()
    {
        // Check if laser has traveled too far
        float distanceTraveled = Vector3.Distance(startPosition, transform.position);
        if (distanceTraveled > maxDistance)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"OnTriggerEnter: {other.gameObject.name}");
        // Check if we hit an asteroid
        if (other.CompareTag("Asteroid"))
        {
            Asteroid asteroid = other.GetComponent<Asteroid>();
            if (asteroid != null)
            {
                asteroid.TakeDamage(damage, rb.mass, rb.velocity, transform.position);
            }
            Destroy(gameObject);
            // Spawn hit effect if we have one
            if (hitEffect != null)
            {
                Instantiate(hitEffect, transform.position, Quaternion.identity);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Boundary"))
        {
            Destroy(gameObject);
        }
    }
} 
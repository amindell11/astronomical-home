using UnityEngine;

public class LaserProjectile : MonoBehaviour
{
    [Header("Laser Properties")]
    [SerializeField] private float damage = 10f;
    [SerializeField] private float maxDistance = 50f;
    [SerializeField] private GameObject hitEffect;
    [SerializeField] private float mass = 0.1f;  // Mass of the laser projectile

    private Vector2 startPosition;
    private Rigidbody2D rb;

    private void Start()
    {
        startPosition = transform.position;
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        // Check if laser has traveled too far
        float distanceTraveled = Vector2.Distance(startPosition, transform.position);
        if (distanceTraveled > maxDistance)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if we hit an asteroid
        if (other.CompareTag("Asteroid"))
        {
            Asteroid asteroid = other.GetComponent<Asteroid>();
            if (asteroid != null)
            {
                asteroid.TakeDamage(damage, rb.mass, rb.velocity);
            }
            Destroy(gameObject);
            // Spawn hit effect if we have one
            if (hitEffect != null)
            {
                Instantiate(hitEffect, transform.position, Quaternion.identity);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Boundary"))
        {
            Destroy(gameObject);
        }
    }
} 
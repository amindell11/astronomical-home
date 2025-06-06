using UnityEngine;
using System.Collections.Generic;

public class Asteroid : MonoBehaviour
{
    [Header("Asteroid Properties")]
    [SerializeField] private float minMass = 0.2f;        // Minimum mass before no more fragmentation
    [SerializeField] private float fragmentMassRatio = 0.5f;  // How much mass each fragment gets
    [SerializeField] private int minFragments = 2;        // Minimum number of fragments
    [SerializeField] private int maxFragments = 4;        // Maximum number of fragments
    [SerializeField] private float fragmentSpeed = 5f;    // Speed of fragments
    [SerializeField] private GameObject explosionPrefab;  // Explosion effect prefab

    [Header("Physics Ranges")]
    [SerializeField] public Vector2 massRange = new Vector2(0.5f, 2f);  // Mass range for new asteroids
    [SerializeField] public Vector2 velocityRange = new Vector2(0.5f, 2f);
    [SerializeField] public Vector2 spinRange = new Vector2(-30f, 30f);

    [Header("Fragmentation Settings")]
    [SerializeField] private float separationBias = 0.5f; // How much fragments bias away from impact
    [SerializeField] private float baseSeparationSpeed = 5f; // Base speed for fragment separation
    [SerializeField] private float spinVariation = 30f;   // Random variation in spin
    [SerializeField] private float explosiveLossFactor = 0.5f; // How much momentum is conserved (0.9 = 10% loss)

    [Header("Visual Settings")]
    [SerializeField] private Sprite[] asteroidSprites;

    [Header("Physics Settings")]
    [SerializeField] private float maxVelocity = 15f;        // Maximum linear velocity
    [SerializeField] private float maxAngularVelocity = 180f; // Maximum angular velocity (degrees per second)

    private float currentMass;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // Initialize asteroid with optional parameters - if null, values are randomized
    public void Initialize(Vector3 position, float? mass = null, Vector2? velocity = null, float? rotation = null, float? angularVelocity = null)
    {
        // Set mass
        currentMass = mass ?? Random.Range(massRange.x, massRange.y);
        UpdatePhysics();
        SetRandomSprite();

        // Set position and rotation
        transform.position = position;
        transform.rotation = Quaternion.Euler(0, 0, rotation ?? Random.Range(0f, 360f));

        // Calculate velocity scale based on mass
        float velocityScale = 1f / Mathf.Pow(currentMass, 1f/3f);
        // Set velocity
        rb.velocity = velocity ?? (Random.insideUnitCircle.normalized * 
            Random.Range(velocityRange.x, velocityRange.y) * velocityScale);

        // Set angular velocity
        rb.angularVelocity = angularVelocity ?? Random.Range(spinRange.x, spinRange.y) * velocityScale;
    }

    private void SetRandomSprite()
    {
        if (asteroidSprites != null && asteroidSprites.Length > 0 && spriteRenderer != null)
        {
            spriteRenderer.sprite = asteroidSprites[Random.Range(0, asteroidSprites.Length)];
        }
    }

    private void UpdatePhysics()
    {
        // Update mass and scale
        rb.mass = currentMass;
        float scale = Mathf.Pow(currentMass, 1f/3f);
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    public void TakeDamage(float damage, float projectileMass, Vector2 projectileVelocity)
    {
        // Calculate if asteroid should fragment
        if (currentMass > minMass)
        {
            Fragment(projectileMass, projectileVelocity);
        }
        else
        {
            // If too small, just explode
            Explode();
            Destroy(gameObject);
        }
    }

    private void Fragment(float projectileMass, Vector2 projectileVelocity)
    {
        // Step 1: Calculate initial momentum
        Vector2 totalLinearMomentum = currentMass * rb.velocity + projectileMass * projectileVelocity;
        float totalAngularMomentum = rb.inertia * rb.angularVelocity;
        
        // Add projectile's angular momentum contribution
        Vector2 hitPoint = transform.position; // For now, assume impact at center
        Vector2 hitOffset = hitPoint - new Vector2(transform.position.x, transform.position.y);
        float projectileAngularMomentum = Vector2.Dot(hitOffset, projectileMass * projectileVelocity);
        totalAngularMomentum += projectileAngularMomentum;

        // Step 2: Calculate fragment masses
        int fragmentCount = Mathf.RoundToInt(
            Mathf.Lerp(minFragments, maxFragments, 
            (currentMass - minMass) / (massRange.y - minMass))
        );

        float[] fragmentMasses = new float[fragmentCount];
        float remainingMass = currentMass;
        
        // Distribute mass among fragments
        for (int i = 0; i < fragmentCount; i++)
        {
            if (i == fragmentCount - 1)
            {
                fragmentMasses[i] = remainingMass;
            }
            else
            {
                float maxPossibleMass = remainingMass - (minMass * (fragmentCount - i - 1));
                fragmentMasses[i] = Random.Range(minMass, maxPossibleMass);
                remainingMass -= fragmentMasses[i];
            }
        }

        // Step 3: Calculate initial separation velocities
        Vector2[] fragmentVelocities = new Vector2[fragmentCount];
        Vector2[] fragmentPositions = new Vector2[fragmentCount];
        
        for (int i = 0; i < fragmentCount; i++)
        {
            // Calculate fragment position (random offset from center)
            Vector2 randomOffset = Random.insideUnitCircle.normalized * 0.5f;
            fragmentPositions[i] = (Vector2)transform.position + randomOffset;
            
            // Calculate separation direction
            Vector2 toCenter = (Vector2)transform.position - fragmentPositions[i];
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            Vector2 separationDir = (randomDir + separationBias * toCenter.normalized).normalized;
            
            // Calculate separation speed
            float relativeSpeed = (projectileVelocity - rb.velocity).magnitude;
            float separationSpeed = baseSeparationSpeed * relativeSpeed * Random.Range(0.8f, 1.2f);
            
            // Set initial velocity relative to asteroid center
            fragmentVelocities[i] = separationDir * separationSpeed;
        }

        // Step 4: Enforce conservation of linear momentum
        Vector2 totalFragmentMomentum = Vector2.zero;
        float totalFragmentMass = 0f;
        
        for (int i = 0; i < fragmentCount; i++)
        {
            // Convert to world velocities
            fragmentVelocities[i] += rb.velocity;
            
            // Sum up momentum
            totalFragmentMomentum += fragmentMasses[i] * fragmentVelocities[i];
            totalFragmentMass += fragmentMasses[i];
        }
        
        // Calculate and apply momentum correction
        Vector2 momentumCorrection = (totalLinearMomentum - totalFragmentMomentum) * explosiveLossFactor / totalFragmentMass;
        for (int i = 0; i < fragmentCount; i++)
        {
            fragmentVelocities[i] += momentumCorrection;
        }

        // Step 5: Calculate and assign rotational spin
        float totalOrbitalAngularMomentum = 0f;
        float totalFragmentInertia = 0f;
        
        for (int i = 0; i < fragmentCount; i++)
        {
            // Calculate orbital angular momentum
            Vector2 r = fragmentPositions[i] - (Vector2)transform.position;
            float orbitalAngularMomentum = Vector2.Dot(r, fragmentMasses[i] * fragmentVelocities[i]);
            totalOrbitalAngularMomentum += orbitalAngularMomentum;
            
            // Calculate fragment inertia (assuming spherical fragments)
            float fragmentRadius = Mathf.Pow(fragmentMasses[i], 1f/3f);
            float fragmentInertia = 0.4f * fragmentMasses[i] * fragmentRadius * fragmentRadius;
            totalFragmentInertia += fragmentInertia;
        }
        
        // Calculate total spin angular momentum
        float totalSpinAngularMomentum = totalAngularMomentum - totalOrbitalAngularMomentum;
        
        // Assign spin to fragments
        float[] fragmentSpins = new float[fragmentCount];
        for (int i = 0; i < fragmentCount; i++)
        {
            float fragmentRadius = Mathf.Pow(fragmentMasses[i], 1f/3f);
            float fragmentInertia = 0.4f * fragmentMasses[i] * fragmentRadius * fragmentRadius;
            
            // Base spin from conservation
            float baseSpin = totalSpinAngularMomentum / totalFragmentInertia;
            
            // Add random variation
            float randomVariation = Random.Range(-spinVariation, spinVariation);
            fragmentSpins[i] = baseSpin + randomVariation;
        }

        // Spawn explosion effect
        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        }

        // Spawn fragments
        for (int i = 0; i < fragmentCount; i++)
        {
            if (AsteroidController.Instance != null)
            {
                AsteroidController.Instance.SpawnAsteroid(
                    fragmentPositions[i],
                    fragmentMasses[i],
                    fragmentVelocities[i],
                    Random.Range(0f, 360f),
                    fragmentSpins[i]
                );
            }
        }

        // Remove from active asteroids list and destroy
        if (AsteroidController.Instance != null)
        {
            AsteroidController.Instance.RemoveAsteroid(gameObject);
        }
        Destroy(gameObject);
    }

    private void Explode()
    {
        // Spawn explosion effect
        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("AsteroidCullingBoundary"))
        {
            // Remove from active asteroids list and destroy
            if (AsteroidController.Instance != null)
            {
                AsteroidController.Instance.RemoveAsteroid(gameObject);
            }
            Destroy(gameObject);
        }
    }
}
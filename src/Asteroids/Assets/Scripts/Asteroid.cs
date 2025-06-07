using UnityEngine;
using System.Collections.Generic;

public class Asteroid : MonoBehaviour
{
    [Header("Asteroid Properties")]
    [SerializeField] private float minMass = 0.2f;        // Minimum mass before no more fragmentation
    [SerializeField] private int minFragments = 2;        // Minimum number of fragments
    [SerializeField] private int maxFragments = 4;        // Maximum number of fragments
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
    [SerializeField] private float outwardBias = 0.7f;    // How much fragments bias outward from center
    [SerializeField] private float bulletBias = 1.0f;     // How much fragments bias in bullet direction
    [SerializeField] private float randomBias = 0.3f;     // How much random direction is added

    [Header("Visual Settings")]
    [SerializeField] private Sprite[] asteroidSprites;
    private float currentMass;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }
    private void Start()
    {
            currentMass = rb.mass;
    }

    public void Initialize(Vector3 position, float? mass = null, Vector2? velocity = null, float? rotation = null, float? angularVelocity = null)
    {
        currentMass = mass ?? Random.Range(massRange.x, massRange.y);
        UpdatePhysics();
        SetRandomSprite();

        transform.position = position;
        transform.rotation = Quaternion.Euler(0, 0, rotation ?? Random.Range(0f, 360f));

        float velocityScale = 1f / Mathf.Pow(currentMass, 1f/3f);
        
        rb.velocity = velocity ?? (Random.insideUnitCircle.normalized * 
            Random.Range(velocityRange.x, velocityRange.y) * velocityScale);
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
        rb.mass = currentMass;
        float scale = Mathf.Pow(currentMass, 1f/3f);
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    public void TakeDamage(float damage, float projectileMass, Vector2 projectileVelocity)
    {
        if (currentMass > minMass)
        {
            Fragment(projectileMass, projectileVelocity);
        }
        else
        {
            Explode();
            Destroy(gameObject);
        }
    }

    private void Fragment(float projectileMass, Vector2 projectileVelocity)
    {
        var (totalLinearMomentum, totalAngularMomentum) = CalculateInitialMomentum(projectileMass, projectileVelocity);
        int fragmentCount = CalculateFragmentCount();
        float[] fragmentMasses = CalculateFragmentMasses(fragmentCount);
        Vector2[] fragmentPositions = CalculateFragmentPositions(fragmentCount);
        
        var (fragmentVelocities, fragmentSpins) = CalculateFragmentPhysics(
            fragmentCount, 
            fragmentMasses, 
            fragmentPositions, 
            totalLinearMomentum, 
            totalAngularMomentum,
            projectileVelocity
        );

        SpawnFragments(fragmentCount, fragmentPositions, fragmentMasses, fragmentVelocities, fragmentSpins);
        Explode();
        CleanupAsteroid();
    }

    private (Vector2 linear, float angular) CalculateInitialMomentum(float projectileMass, Vector2 projectileVelocity)
    {
        Vector2 asteroidMomentum = currentMass * rb.velocity;
        Vector2 projectileMomentum = projectileMass * projectileVelocity;
        Vector2 totalLinearMomentum = asteroidMomentum + projectileMomentum;
        
        float asteroidAngularMomentum = rb.inertia * rb.angularVelocity;
        
        Vector2 hitPoint = transform.position;
        Vector2 hitOffset = hitPoint - new Vector2(transform.position.x, transform.position.y);
        float projectileAngularMomentum = Vector2.Dot(hitOffset, projectileMass * projectileVelocity);
        float totalAngularMomentum = asteroidAngularMomentum + projectileAngularMomentum;

        return (totalLinearMomentum, totalAngularMomentum);
    }

    private int CalculateFragmentCount()
    {
        float massRatio = (currentMass - minMass) / (massRange.y - minMass);
        float countFloat = Mathf.Lerp(minFragments, maxFragments, massRatio);
        int count = Mathf.RoundToInt(countFloat);
        return count;
    }

    private float[] CalculateFragmentMasses(int fragmentCount)
    {
        float[] fragmentMasses = new float[fragmentCount];
        float remainingMass = currentMass;
        
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
        return fragmentMasses;
    }

    private Vector2[] CalculateFragmentPositions(int fragmentCount)
    {
        Vector2[] positions = new Vector2[fragmentCount];
        for (int i = 0; i < fragmentCount; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle.normalized * 0.5f;
            positions[i] = (Vector2)transform.position + randomOffset;
        }
        return positions;
    }

    private (Vector2[] velocities, float[] spins) CalculateFragmentPhysics(
        int fragmentCount, 
        float[] fragmentMasses, 
        Vector2[] fragmentPositions, 
        Vector2 totalLinearMomentum, 
        float totalAngularMomentum,
        Vector2 projectileVelocity
        )
    {
        Vector2[] fragmentVelocities = new Vector2[fragmentCount];
        float[] fragmentSpins = new float[fragmentCount];

        for (int i = 0; i < fragmentCount; i++)
        {
            Vector2 fromCenter = fragmentPositions[i] - (Vector2)transform.position;
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            Vector2 bulletDir = projectileVelocity.normalized;
            
                        // Combine the different bias components
            Vector2 separationDir = (
                outwardBias * fromCenter.normalized + 
                bulletBias * bulletDir + 
                randomBias * randomDir
            ).normalized;

        float relativeSpeed = (projectileVelocity - rb.velocity).magnitude;
        float separationSpeed = baseSeparationSpeed * relativeSpeed *
                                Random.Range(0.8f, 1.2f);            
            fragmentVelocities[i] = (separationDir * separationSpeed)+ rb.velocity;
        }

        Vector2 totalFragmentMomentum = Vector2.zero;
        float totalFragmentMass = 0f;

        
        for (int i = 0; i < fragmentCount; i++)
        {
            totalFragmentMomentum += fragmentMasses[i] * fragmentVelocities[i];
            totalFragmentMass += fragmentMasses[i];
        }
        
        Vector2 momentumCorrection = (totalLinearMomentum - totalFragmentMomentum) * explosiveLossFactor / totalFragmentMass;
        for (int i = 0; i < fragmentCount; i++)
        {
            fragmentVelocities[i] += momentumCorrection;
        }

        float totalOrbitalAngularMomentum = 0f;
        float totalFragmentInertia = 0f;
        
        for (int i = 0; i < fragmentCount; i++)
        {
            Vector2 r = fragmentPositions[i] - (Vector2)transform.position;
            float orbitalAngularMomentum = Vector2.Dot(r, fragmentMasses[i] * fragmentVelocities[i]);
            totalOrbitalAngularMomentum += orbitalAngularMomentum;
            
            float fragmentRadius = Mathf.Pow(fragmentMasses[i], 1f/3f);
            float fragmentInertia = 0.4f * fragmentMasses[i] * fragmentRadius * fragmentRadius;
            totalFragmentInertia += fragmentInertia;
        }
        
        float totalSpinAngularMomentum = totalAngularMomentum - totalOrbitalAngularMomentum;
        
        for (int i = 0; i < fragmentCount; i++)
        {
            float fragmentRadius = Mathf.Pow(fragmentMasses[i], 1f/3f);
            float fragmentInertia = 0.4f * fragmentMasses[i] * fragmentRadius * fragmentRadius;
            
            float baseSpin = totalSpinAngularMomentum / totalFragmentInertia;
            float randomVariation = Random.Range(-spinVariation, spinVariation);
            fragmentSpins[i] = baseSpin + randomVariation;
        }
        return (fragmentVelocities, fragmentSpins);
    }

    private void SpawnFragments(int fragmentCount, Vector2[] positions, float[] masses, Vector2[] velocities, float[] spins)
    {
        for (int i = 0; i < fragmentCount; i++)
        {
            if (AsteroidController.Instance != null)
            {
                GameObject fragment = AsteroidController.Instance.SpawnAsteroid(
                    positions[i],
                    masses[i],
                    velocities[i],
                    Random.Range(0f, 360f),
                    spins[i]
                );
            }
        }
    }

    private void CleanupAsteroid()
    {
        if (AsteroidController.Instance != null)
        {
            AsteroidController.Instance.ActiveAsteroids.Remove(gameObject);
        }
        Destroy(gameObject);
    }

    private void Explode()
    {
        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("AsteroidCullingBoundary"))
        {
            CleanupAsteroid();
        }
    }

    private void OnDrawGizmos()
    {
        if (rb != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 velocity = rb.velocity;
            Vector3 start = transform.position;
            Vector3 end = start + (Vector3)velocity.normalized * 2f;
            Gizmos.DrawLine(start, end);
            
            // Draw arrow head
            Vector3 right = Quaternion.Euler(0, 0, 30) * -velocity.normalized * (2f * 0.2f);
            Vector3 left = Quaternion.Euler(0, 0, -30) * -velocity.normalized * (2f * 0.2f);
            Gizmos.DrawLine(end, end + right);
            Gizmos.DrawLine(end, end + left);
        }
    }
}
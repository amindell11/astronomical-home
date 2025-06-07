using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public class Fragnetics : MonoBehaviour
{
    public static Fragnetics Instance { get; private set; }

    [Header("Fragmentation Settings")]
    [SerializeField]
    [Tooltip("Minimum mass threshold before an asteroid stops fragmenting")]
    private float minMass = 30f;
    
    [SerializeField] 
    [Tooltip("Minimum number of fragments created when an asteroid breaks")]
    private int minFragments = 2;
    
    [SerializeField]
    [Tooltip("Maximum number of fragments created when an asteroid breaks")] 
    private int maxFragments = 4;
    
    [SerializeField, Range(0.01f, 0.99f)]
    [Tooltip("Controls bias toward maximum fragments (0 = minimum, 1 = maximum)")]
    private float highCountBias = 0.5f;
    
    [SerializeField]
    [Tooltip("How strongly fragments move away from the impact point")]
    private float separationBias = 0.5f;
    
    [SerializeField]
    [Tooltip("Base velocity for fragment separation")]
    private float baseSeparationSpeed = 5f;
    
    [SerializeField]
    [Tooltip("Maximum random rotation speed added to fragments in degrees/sec")]
    private float spinVariation = 30f;
    
    [SerializeField]
    [Tooltip("Fraction of momentum preserved in the explosion (1 = perfect conservation)")]
    private float explosiveLossFactor = 0.5f;
    
    [SerializeField]
    [Tooltip("How strongly fragments move away from the asteroid's center")]
    private float outwardBias = 0.7f;
    
    [SerializeField]
    [Tooltip("How strongly fragments follow the direction of the impacting projectile")]
    private float bulletBias = 1.0f;
    
    [SerializeField]
    [Tooltip("Amount of random variation added to fragment directions")]
    private float randomBias = 0.3f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }
    public void CreateFragments(
        Asteroid asteroid,
        float projectileMass,
        Vector3 projectileVelocity,
        Vector3 hitPoint
    )
    {
        var (totalLinearMomentum, totalAngularMomentum) = CalculateInitialMomentum(asteroid, projectileMass, projectileVelocity, hitPoint);
        
        float[] fragmentMasses = GenerateFragmentMasses(asteroid.CurrentMass);
        int fragmentCount = fragmentMasses.Length;
        if (fragmentCount <= 0) return;

        Vector3[] fragmentPositions = CalculateFragmentPositions(asteroid.transform.position, fragmentCount);
        
        var (fragmentVelocities, fragmentSpins) = CalculateFragmentPhysics(
            asteroid,
            fragmentCount, 
            fragmentMasses, 
            fragmentPositions, 
            totalLinearMomentum, 
            totalAngularMomentum,
            projectileVelocity
        );

        SpawnFragments(fragmentCount, fragmentPositions, fragmentMasses, fragmentVelocities, fragmentSpins);
    }
    
    /// <summary>
    /// Returns an array of fragment masses that:
    ///   - each ≥ minMass
    ///   - count is between minFragments and maxFragments
    ///   - total = totalMass
    ///   - biased toward using more fragments when possible
    /// Returns an empty array if not enough mass to create minFragments.
    /// </summary>
    private float[] GenerateFragmentMasses(float totalMass)
    {
        // Determine the feasible number of fragments
        if (totalMass <= 0 || minMass <= 0) return Array.Empty<float>();
        int feasibleMax = Mathf.Min(maxFragments, Mathf.FloorToInt(totalMass / minMass));
        if (feasibleMax < minFragments) return Array.Empty<float>();

        // Choose a fragment count, biased toward the high end
        float randomBiased = Mathf.Pow(UnityEngine.Random.value, highCountBias);
        int n = minFragments + Mathf.FloorToInt(randomBiased * (feasibleMax - minFragments + 1));

        // Slice totalMass into n parts using a Dirichlet distribution
        float remainingMass = totalMass - n * minMass;
        if (remainingMass < 0) remainingMass = 0;

        // Generate n random weights
        var weights = Enumerable.Range(0, n)
                                .Select(_ => UnityEngine.Random.value)
                                .ToArray();
        float sumOfWeights = weights.Sum();

        // If the sum of weights is zero (highly unlikely), distribute the remaining mass equally
        if (sumOfWeights == 0)
        {
            float extraPerFragment = remainingMass / n;
            return Enumerable.Repeat(minMass + extraPerFragment, n).ToArray();
        }

        // Distribute the remaining mass according to the weights
        return weights.Select(w => minMass + (w / sumOfWeights) * remainingMass).ToArray();
    }


    private (Vector3 linear, Vector3 angular) CalculateInitialMomentum(Asteroid asteroid, float projectileMass, Vector3 projectileVelocity, Vector3 hitPoint)
    {
        Vector3 asteroidMomentum = asteroid.CurrentMass * asteroid.Rb.velocity;
        Vector3 projectileMomentum = projectileMass * projectileVelocity;
        Vector3 totalLinearMomentum = asteroidMomentum + projectileMomentum;
        
        Vector3 localAngularVelocity = Quaternion.Inverse(asteroid.transform.rotation) * asteroid.Rb.angularVelocity;
        Vector3 localAngularMomentum = Vector3.Scale(asteroid.Rb.inertiaTensor, localAngularVelocity);
        Vector3 asteroidAngularMomentum = asteroid.transform.rotation * localAngularMomentum;

        Vector3 r = hitPoint - asteroid.transform.position;
        Vector3 projectileAngularMomentum = Vector3.Cross(r, projectileMomentum);
        Vector3 totalAngularMomentum = asteroidAngularMomentum + projectileAngularMomentum;

        return (totalLinearMomentum, totalAngularMomentum);
    }

    private Vector3[] CalculateFragmentPositions(Vector3 parentPosition, int fragmentCount)
    {
        Vector3[] positions = new Vector3[fragmentCount];
        for (int i = 0; i < fragmentCount; i++)
        {
            Vector3 randomOffset = UnityEngine.Random.insideUnitCircle.normalized * 0.5f;
            positions[i] = parentPosition + randomOffset;
        }
        return positions;
    }

    (Vector3[] velocities, Vector3[] spins) CalculateFragmentPhysics(
    Asteroid      asteroid,
    int           fragmentCount,
    float[]       masses,          // masses            [n]
    Vector3[]     positions,        // spawn positions   [n]
    Vector3       P_total,    // total linear mom. before break
    Vector3       L_total,    // total angular mom.
    Vector3       vBullet)    // projectile velocity (world)
{
    var v     = new Vector3[fragmentCount];
    var ω     = new Vector3[fragmentCount];
    var spinJitter = new Vector3[fragmentCount];

    /* ───────── pass #1 : build raw velocities & gather sums ───────── */
    Vector3 center   = asteroid.transform.position;
    Vector3 vAst     = asteroid.Rb.velocity;
    Vector3 bulletDir= (vBullet - vAst).normalized;
    float   relSpeed = (vBullet - vAst).magnitude;

    float   M_tot    = 0f;
    Vector3 P_frag   = Vector3.zero;
    Vector3 Mr_sum   = Vector3.zero;
    Vector3 L_orbit  = Vector3.zero;
    float   I_tot    = 0f;

    for (int i = 0; i < fragmentCount; ++i)
    {
        /* ---- directional kick ---- */
        Vector3 outward = (positions[i] - center).normalized;
        Vector3 random  = UnityEngine.Random.insideUnitSphere.normalized;

        Vector3 dir = (outwardBias * outward +
                       bulletBias  * bulletDir +
                       randomBias  * random).normalized;

        float speed = baseSeparationSpeed * relSpeed
                    * UnityEngine.Random.Range(0.8f, 1.2f);

        v[i] = vAst + dir * speed;

        /* ---- accumulate for momentum bookkeeping ---- */
        M_tot        += masses[i];
        P_frag       += masses[i] * v[i];

        Vector3 r     = positions[i] - center;
        Mr_sum       += masses[i] * r;
        L_orbit      += Vector3.Cross(r, masses[i] * v[i]);

        float radius  = Mathf.Pow(masses[i], 1f / 3f);          // ~m^(1/3)
        I_tot        += 0.4f * masses[i] * radius * radius;

        /* ---- store per-piece spin noise ---- */
        spinJitter[i] = UnityEngine.Random.insideUnitSphere * spinVariation;
    }

    /* ───────── momentum correction (single vector) ───────── */
    Vector3 vCorr = (P_total - P_frag) * explosiveLossFactor / M_tot;

    /* adjust orbital L by analytical Δ (mass-weighted COM offset × vCorr) */
    L_orbit += Vector3.Cross(Mr_sum, vCorr);

    /* ───────── compute common base spin ω_base ───────── */
    Vector3 L_spin  = (L_total - L_orbit) * explosiveLossFactor;
    Vector3 ω_base  = I_tot > 0f ? L_spin / I_tot : Vector3.zero;

    /* ───────── pass #2 : apply correction & finalise spin ───────── */
    for (int i = 0; i < fragmentCount; ++i)
    {
        v[i]    += vCorr;
        ω[i]     = ω_base + spinJitter[i];
    }

    return (v, ω);
}
    private void SpawnFragments(int fragmentCount, Vector3[] positions, float[] masses, Vector3[] velocities, Vector3[] spins)
    {
        for (int i = 0; i < fragmentCount; i++)
        {
            if (AsteroidSpawner.Instance != null)
            {
                Pose spawnPose = new Pose(positions[i], UnityEngine.Random.rotationUniform);
                AsteroidSpawner.Instance.SpawnAsteroid(
                    spawnPose,
                    masses[i],
                    velocities[i],
                    spins[i]
                );
            }
        }
    }
}

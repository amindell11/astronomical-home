using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System;

/// <summary>
/// Helper class to store fragment physics calculation results
/// </summary>
public class FragmentPhysicsResult
{
    public Vector3[] velocities;
    public Vector3[] spins;
    
    public FragmentPhysicsResult(Vector3[] velocities, Vector3[] spins)
    {
        this.velocities = velocities;
        this.spins = spins;
    }
}

public class AsteroidFragnetics : MonoBehaviour
{
    public static AsteroidFragnetics Instance { get; private set; }

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

    [SerializeField, Range(0f, 1f)]
    [Tooltip("Fraction of asteroid mass preserved in fragments (e.g., 0.8 = 80% mass retained, 0.2 lost)")]
    private float massLossFactor = 1.0f;

    [Header("Performance Settings")]
    [SerializeField]
    [Tooltip("Use coroutine version to spread physics calculations across frames")]
    private bool useCoroutineVersion = true;

    [Header("Visual Smoothing")]
    [SerializeField]
    [Tooltip("Spawn placeholder fragments immediately to mask coroutine delay")]
    private bool usePlaceholderFragments = true;

    [SerializeField]
    [Tooltip("How long to fade in fragments after spawning (0 = instant)")]
    private float fragmentFadeInTime = 0.1f;

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

    /// <summary>
    /// Public entry point for fragment creation - automatically chooses coroutine or direct version
    /// </summary>
    public void CreateFragments(
        Asteroid asteroid,
        float projectileMass,
        Vector3 projectileVelocity,
        Vector3 hitPoint
    )
    {
        CreateFragments(asteroid, projectileMass, projectileVelocity, hitPoint, null);
    }

    /// <summary>
    /// Public entry point with explosion callback for delayed explosion option
    /// </summary>
    public void CreateFragments(
        Asteroid asteroid,
        float projectileMass,
        Vector3 projectileVelocity,
        Vector3 hitPoint,
        System.Action onExplosionReady
    )
    {
        if (useCoroutineVersion)
        {
            if (usePlaceholderFragments)
            {
                StartCoroutine(CreateFragmentsWithPlaceholders(asteroid, projectileMass, projectileVelocity, hitPoint));
                // Explosion happens immediately with placeholders
                onExplosionReady?.Invoke();
            }
            else
            {
                StartCoroutine(CreateFragmentsCoroutine(asteroid, projectileMass, projectileVelocity, hitPoint, onExplosionReady));
            }
        }
        else
        {
            CreateFragmentsDirect(asteroid, projectileMass, projectileVelocity, hitPoint);
            // Explosion happens immediately with direct version
            onExplosionReady?.Invoke();
        }
    }

    /// <summary>
    /// Coroutine version that spreads heavy calculations across multiple frames
    /// </summary>
    public IEnumerator CreateFragmentsCoroutine(
        Asteroid asteroid,
        float projectileMass,
        Vector3 projectileVelocity,
        Vector3 hitPoint,
        System.Action onExplosionReady = null
    )
    {
        var (totalLinearMomentum, totalAngularMomentum) = CalculateInitialMomentum(asteroid, projectileMass, projectileVelocity, hitPoint);
        
        float[] fragmentMasses = GenerateFragmentMasses(asteroid.CurrentMass * massLossFactor);
        int fragmentCount = fragmentMasses.Length;
        if (fragmentCount <= 0) 
        {
            onExplosionReady?.Invoke();
            yield break;
        }

        Vector3[] fragmentPositions = CalculateFragmentPositions(asteroid.transform.position, fragmentCount);
        
        // Yield before heavy physics calculations
        yield return null;
        
        // Use a coroutine wrapper to get the results
        FragmentPhysicsResult result = null;
        yield return StartCoroutine(CalculateFragmentPhysicsCoroutine(
            asteroid,
            fragmentCount, 
            fragmentMasses, 
            fragmentPositions, 
            totalLinearMomentum, 
            totalAngularMomentum,
            projectileVelocity,
            (r) => result = r
        ));

        if (result != null)
        {
            SpawnFragments(fragmentCount, fragmentPositions, fragmentMasses, result.velocities, result.spins);
        }

        // Trigger explosion after fragments are spawned
        onExplosionReady?.Invoke();
    }

    /// <summary>
    /// Direct (non-coroutine) version for backward compatibility or when frame spreading isn't needed
    /// </summary>
    private void CreateFragmentsDirect(
        Asteroid asteroid,
        float projectileMass,
        Vector3 projectileVelocity,
        Vector3 hitPoint
    )
    {
        var (totalLinearMomentum, totalAngularMomentum) = CalculateInitialMomentum(asteroid, projectileMass, projectileVelocity, hitPoint);
        
        float[] fragmentMasses = GenerateFragmentMasses(asteroid.CurrentMass * massLossFactor);
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
    /// Coroutine version of fragment physics calculation that yields between heavy computation passes
    /// </summary>
    private IEnumerator CalculateFragmentPhysicsCoroutine(
        Asteroid asteroid,
        int fragmentCount,
        float[] masses,
        Vector3[] positions,
        Vector3 P_total,
        Vector3 L_total,
        Vector3 vBullet,
        System.Action<FragmentPhysicsResult> onComplete
    )
    {
        var velocities = new Vector3[fragmentCount];
        var spins = new Vector3[fragmentCount];
        var spinJitter = new Vector3[fragmentCount];

        /* ───────── pass #1 : build raw velocities & gather sums ───────── */
        Vector3 center = asteroid.transform.position;
        Vector3 vAst = asteroid.Rb.velocity;
        Vector3 bulletDir = (vBullet - vAst).normalized;
        float relSpeed = (vBullet - vAst).magnitude;

        float M_tot = 0f;
        Vector3 P_frag = Vector3.zero;
        Vector3 Mr_sum = Vector3.zero;
        Vector3 L_orbit = Vector3.zero;
        float I_tot = 0f;

        // Heavy computation pass #1 - process fragments in chunks to avoid hitches
        int chunkSize = Mathf.Max(1, fragmentCount / 2); // Process in 2 chunks max
        for (int chunkStart = 0; chunkStart < fragmentCount; chunkStart += chunkSize)
        {
            int chunkEnd = Mathf.Min(chunkStart + chunkSize, fragmentCount);
            
            for (int i = chunkStart; i < chunkEnd; ++i)
            {
                /* ---- directional kick ---- */
                Vector3 outward = (positions[i] - center).normalized;
                Vector3 random = UnityEngine.Random.insideUnitSphere.normalized;

                Vector3 dir = (outwardBias * outward +
                               bulletBias * bulletDir +
                               randomBias * random).normalized;

                float speed = baseSeparationSpeed * relSpeed
                            * UnityEngine.Random.Range(0.8f, 1.2f);

                velocities[i] = vAst + dir * speed;

                /* ---- accumulate for momentum bookkeeping ---- */
                M_tot += masses[i];
                P_frag += masses[i] * velocities[i];

                Vector3 r = positions[i] - center;
                Mr_sum += masses[i] * r;
                L_orbit += Vector3.Cross(r, masses[i] * velocities[i]);

                float radius = Mathf.Pow(masses[i], 1f / 3f);
                I_tot += 0.4f * masses[i] * radius * radius;

                /* ---- store per-piece spin noise ---- */
                spinJitter[i] = UnityEngine.Random.insideUnitSphere * spinVariation;
            }
            
            // Yield after processing each chunk to spread work across frames
            if (chunkEnd < fragmentCount)
            {
                yield return null;
            }
        }

        /* ───────── momentum correction (single vector) ───────── */
        Vector3 vCorr = (P_total - P_frag) * explosiveLossFactor / M_tot;

        /* adjust orbital L by analytical Δ (mass-weighted COM offset × vCorr) */
        L_orbit += Vector3.Cross(Mr_sum, vCorr);

        /* ───────── compute common base spin ω_base ───────── */
        Vector3 L_spin = (L_total - L_orbit) * explosiveLossFactor;
        Vector3 ω_base = I_tot > 0f ? L_spin / I_tot : Vector3.zero;

        // Yield before second pass
        yield return null;

        /* ───────── pass #2 : apply correction & finalise spin ───────── */
        for (int i = 0; i < fragmentCount; ++i)
        {
            velocities[i] += vCorr;
            spins[i] = ω_base + spinJitter[i];
        }

        // Return results through callback
        onComplete?.Invoke(new FragmentPhysicsResult(velocities, spins));
    }

    /// <summary>
    /// Original synchronous version of fragment physics calculation (for direct version)
    /// </summary>
    private (Vector3[] velocities, Vector3[] spins) CalculateFragmentPhysics(
        Asteroid asteroid,
        int fragmentCount,
        float[] masses,
        Vector3[] positions,
        Vector3 P_total,
        Vector3 L_total,
        Vector3 vBullet
    )
    {
        var velocities = new Vector3[fragmentCount];
        var spins = new Vector3[fragmentCount];
        var spinJitter = new Vector3[fragmentCount];

        /* ───────── pass #1 : build raw velocities & gather sums ───────── */
        Vector3 center = asteroid.transform.position;
        Vector3 vAst = asteroid.Rb.velocity;
        Vector3 bulletDir = (vBullet - vAst).normalized;
        float relSpeed = (vBullet - vAst).magnitude;

        float M_tot = 0f;
        Vector3 P_frag = Vector3.zero;
        Vector3 Mr_sum = Vector3.zero;
        Vector3 L_orbit = Vector3.zero;
        float I_tot = 0f;

        for (int i = 0; i < fragmentCount; ++i)
        {
            /* ---- directional kick ---- */
            Vector3 outward = (positions[i] - center).normalized;
            Vector3 random = UnityEngine.Random.insideUnitSphere.normalized;

            Vector3 dir = (outwardBias * outward +
                           bulletBias * bulletDir +
                           randomBias * random).normalized;

            float speed = baseSeparationSpeed * relSpeed
                        * UnityEngine.Random.Range(0.8f, 1.2f);

            velocities[i] = vAst + dir * speed;

            /* ---- accumulate for momentum bookkeeping ---- */
            M_tot += masses[i];
            P_frag += masses[i] * velocities[i];

            Vector3 r = positions[i] - center;
            Mr_sum += masses[i] * r;
            L_orbit += Vector3.Cross(r, masses[i] * velocities[i]);

            float radius = Mathf.Pow(masses[i], 1f / 3f);
            I_tot += 0.4f * masses[i] * radius * radius;

            /* ---- store per-piece spin noise ---- */
            spinJitter[i] = UnityEngine.Random.insideUnitSphere * spinVariation;
        }

        /* ───────── momentum correction (single vector) ───────── */
        Vector3 vCorr = (P_total - P_frag) * explosiveLossFactor / M_tot;

        /* adjust orbital L by analytical Δ (mass-weighted COM offset × vCorr) */
        L_orbit += Vector3.Cross(Mr_sum, vCorr);

        /* ───────── compute common base spin ω_base ───────── */
        Vector3 L_spin = (L_total - L_orbit) * explosiveLossFactor;
        Vector3 ω_base = I_tot > 0f ? L_spin / I_tot : Vector3.zero;

        /* ───────── pass #2 : apply correction & finalise spin ───────── */
        for (int i = 0; i < fragmentCount; ++i)
        {
            velocities[i] += vCorr;
            spins[i] = ω_base + spinJitter[i];
        }

        return (velocities, spins);
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

    private void SpawnFragments(int fragmentCount, Vector3[] positions, float[] masses, Vector3[] velocities, Vector3[] spins)
    {
        for (int i = 0; i < fragmentCount; i++)
        {
            Debug.Log("Spawning fragment " + i);
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

    /// <summary>
    /// Coroutine version that spawns placeholder fragments immediately, then updates them with proper physics
    /// </summary>
    private IEnumerator CreateFragmentsWithPlaceholders(
        Asteroid asteroid,
        float projectileMass,
        Vector3 projectileVelocity,
        Vector3 hitPoint
    )
    {
        var (totalLinearMomentum, totalAngularMomentum) = CalculateInitialMomentum(asteroid, projectileMass, projectileVelocity, hitPoint);
        
        float[] fragmentMasses = GenerateFragmentMasses(asteroid.CurrentMass * massLossFactor);
        int fragmentCount = fragmentMasses.Length;
        if (fragmentCount <= 0) yield break;

        Vector3[] fragmentPositions = CalculateFragmentPositions(asteroid.transform.position, fragmentCount);
        
        // Spawn placeholder fragments immediately with rough physics
        GameObject[] placeholderFragments = SpawnPlaceholderFragments(
            fragmentCount, 
            fragmentPositions, 
            fragmentMasses, 
            asteroid, 
            projectileVelocity
        );
        
        // Yield before heavy physics calculations
        yield return null;
        
        // Calculate proper physics
        FragmentPhysicsResult result = null;
        yield return StartCoroutine(CalculateFragmentPhysicsCoroutine(
            asteroid,
            fragmentCount, 
            fragmentMasses, 
            fragmentPositions, 
            totalLinearMomentum, 
            totalAngularMomentum,
            projectileVelocity,
            (r) => result = r
        ));

        // Update placeholder fragments with proper physics
        if (result != null && placeholderFragments != null)
        {
            UpdatePlaceholderFragments(placeholderFragments, result.velocities, result.spins);
        }
    }

    /// <summary>
    /// Spawn fragments immediately with rough physics for visual continuity
    /// </summary>
    private GameObject[] SpawnPlaceholderFragments(
        int fragmentCount, 
        Vector3[] positions, 
        float[] masses, 
        Asteroid parentAsteroid, 
        Vector3 projectileVelocity
    )
    {
        GameObject[] fragments = new GameObject[fragmentCount];
        Vector3 baseVelocity = parentAsteroid.Rb.velocity;
        Vector3 impactDirection = (projectileVelocity - baseVelocity).normalized;

        for (int i = 0; i < fragmentCount; i++)
        {
            if (AsteroidSpawner.Instance != null)
            {
                Pose spawnPose = new Pose(positions[i], UnityEngine.Random.rotationUniform);
                
                // Create rough velocity for immediate visual feedback
                Vector3 roughDirection = (positions[i] - parentAsteroid.transform.position).normalized;
                Vector3 roughVelocity = baseVelocity + 
                    (roughDirection * baseSeparationSpeed * 0.5f) + 
                    (impactDirection * baseSeparationSpeed * 0.3f);
                
                Vector3 roughSpin = UnityEngine.Random.insideUnitSphere * spinVariation * 0.5f;

                GameObject fragment = AsteroidSpawner.Instance.SpawnAsteroid(
                    spawnPose,
                    masses[i],
                    roughVelocity,
                    roughSpin
                );

                fragments[i] = fragment;

                // Make fragment initially semi-transparent if we have a fade-in time
                if (fragmentFadeInTime > 0f && fragment != null)
                {
                    StartCoroutine(FadeInFragment(fragment));
                }
            }
        }

        return fragments;
    }

    /// <summary>
    /// Update placeholder fragments with proper physics calculations
    /// </summary>
    private void UpdatePlaceholderFragments(GameObject[] fragments, Vector3[] velocities, Vector3[] spins)
    {
        for (int i = 0; i < fragments.Length; i++)
        {
            if (fragments[i] != null)
            {
                Rigidbody rb = fragments[i].GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = velocities[i];
                    rb.angularVelocity = spins[i];
                }
            }
        }
    }

    /// <summary>
    /// Fade in a fragment over time for smoother visual transition
    /// </summary>
    private IEnumerator FadeInFragment(GameObject fragment)
    {
        if (fragment == null || fragmentFadeInTime <= 0f) yield break;

        Renderer renderer = fragment.GetComponent<Renderer>();
        if (renderer == null) yield break;

        Material material = renderer.material;
        Color originalColor = material.color;
        Color transparentColor = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
        
        material.color = transparentColor;

        float elapsed = 0f;
        while (elapsed < fragmentFadeInTime)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, originalColor.a, elapsed / fragmentFadeInTime);
            material.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            yield return null;
        }

        material.color = originalColor;
    }
}

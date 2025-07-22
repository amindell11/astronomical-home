using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Central registry that tracks all live asteroids in the scene and maintains a running
/// total of their volumes.  This avoids scattering book-keeping logic across spawners,
/// field managers, and fragmenters.
/// </summary>
public class AsteroidRegistry : MonoBehaviour
{
    public static AsteroidRegistry Instance { get; private set; }

    private readonly HashSet<Asteroid> activeAsteroids = new HashSet<Asteroid>();
    private readonly Dictionary<Asteroid, float> trackedVolumes = new Dictionary<Asteroid, float>();

    public IReadOnlyCollection<Asteroid> ActiveAsteroids => activeAsteroids;
    public int ActiveCount => activeAsteroids.Count;
    public float TotalVolume { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Register a newly spawned asteroid. If the asteroid was already registered (e.g. pooled
    /// object reused before OnDisable fired), the volume delta is reconciled.
    /// </summary>
    public void Register(Asteroid asteroid)
    {
        if (asteroid == null) return;

        if (activeAsteroids.Add(asteroid))
        {
            float v = asteroid.CurrentVolume;
            trackedVolumes[asteroid] = v;
            TotalVolume += v;
        }
        else
        {
            // Already present – adjust volume if its scale/mesh changed.
            float oldV = trackedVolumes.TryGetValue(asteroid, out float stored) ? stored : 0f;
            float newV = asteroid.CurrentVolume;
            if (!Mathf.Approximately(oldV, newV))
            {
                trackedVolumes[asteroid] = newV;
                TotalVolume += (newV - oldV);
            }
        }
    }

    /// <summary>
    /// Unregister an asteroid that is leaving the active set.
    /// </summary>
    public void Unregister(Asteroid asteroid)
    {
        if (asteroid == null) return;

        if (activeAsteroids.Remove(asteroid))
        {
            float v = trackedVolumes.TryGetValue(asteroid, out float stored) ? stored : asteroid.CurrentVolume;
            trackedVolumes.Remove(asteroid);
            TotalVolume -= v;
        }
    }
} 
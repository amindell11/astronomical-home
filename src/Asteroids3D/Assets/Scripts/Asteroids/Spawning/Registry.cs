using System.Collections.Generic;
using UnityEngine;

namespace Asteroids.Spawning
{

    public class Registry
    {
        private readonly HashSet<Asteroid> activeAsteroids = new();
        private readonly Dictionary<Asteroid, float> trackedVolumes = new();
        public IReadOnlyCollection<Asteroid> ActiveAsteroids => activeAsteroids;
        public int ActiveCount => activeAsteroids.Count;
        public float TotalVolume { get; private set; }

        public void Register(Asteroid asteroid)
        {
            if (!asteroid) return;

            if (activeAsteroids.Add(asteroid))
            {
                float v = asteroid.Volume;
                trackedVolumes[asteroid] = v;
                TotalVolume += v;
            }
            else
            {
                float oldV = trackedVolumes.GetValueOrDefault(asteroid, 0f);
                float newV = asteroid.Volume;
                if (Mathf.Approximately(oldV, newV)) return;
                trackedVolumes[asteroid] = newV;
                TotalVolume += (newV - oldV);
            }
        }
        public void Unregister(Asteroid asteroid)
        {
            if (!asteroid || !activeAsteroids.Remove(asteroid)) return;
            float v = trackedVolumes.TryGetValue(asteroid, out float stored) ? stored : asteroid.Volume;
            trackedVolumes.Remove(asteroid);
            TotalVolume -= v;
        }
    }
} 
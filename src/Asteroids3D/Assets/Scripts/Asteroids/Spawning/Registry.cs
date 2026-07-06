using System.Collections.Generic;
using UnityEngine;

namespace Asteroids.Spawning
{

    public class Registry
    {
        private readonly HashSet<AsteroidController> activeAsteroids = new();
        private readonly Dictionary<AsteroidController, float> trackedVolumes = new();
        public IReadOnlyCollection<AsteroidController> ActiveAsteroids => activeAsteroids;

        /// <summary>
        /// Concrete live set for hot-path enumeration (the interface view boxes its enumerator).
        /// Read-only by convention — mutate only via Register/Unregister.
        /// </summary>
        public HashSet<AsteroidController> LiveSet => activeAsteroids;
        public int ActiveCount => activeAsteroids.Count;
        public float TotalVolume { get; private set; }

        public void Register(AsteroidController asteroid)
        {
            if (!asteroid) return;

            if (activeAsteroids.Add(asteroid))
            {
                var v = asteroid.Volume;
                trackedVolumes[asteroid] = v;
                TotalVolume += v;
            }
            else
            {
                var oldV = trackedVolumes.GetValueOrDefault(asteroid, 0f);
                var newV = asteroid.Volume;
                if (Mathf.Approximately(oldV, newV)) return;
                trackedVolumes[asteroid] = newV;
                TotalVolume += (newV - oldV);
            }
        }
        public void Unregister(AsteroidController asteroid)
        {
            if (!asteroid || !activeAsteroids.Remove(asteroid)) return;
            var v = trackedVolumes.TryGetValue(asteroid, out var stored) ? stored : asteroid.Volume;
            trackedVolumes.Remove(asteroid);
            TotalVolume -= v;
        }
    }
} 

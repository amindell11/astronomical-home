using Diagnostics.Performance;
using UnityEngine;

namespace Asteroids.Fields
{
    public partial class UpdatingAsteroidField
    {
        public void ApplyLatencyProfilingSettings(LatencyProfilingSettings profilingSettings)
        {
            if (profilingSettings == null || settings == null)
                return;

            // AsteroidCount < 0 means use the prefab's default settings
            if (profilingSettings.AsteroidCount < 0)
                return;

            settings = Instantiate(settings);
            settings.maxAsteroids = profilingSettings.AsteroidCount;
            settings.maxSpawnsPerFrame = Mathf.Max(1, profilingSettings.AsteroidCount);
            settings.densityCheckInterval = Mathf.Max(0.05f, settings.densityCheckInterval);
            CacheSettings();
        }
    }
}

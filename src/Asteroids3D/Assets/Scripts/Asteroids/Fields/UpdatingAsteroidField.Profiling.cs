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

            settings = Instantiate(settings);
            settings.maxAsteroids = profilingSettings.AsteroidCount;
            settings.targetVolumeDensity = Mathf.Max(0.0001f, profilingSettings.AsteroidCount / (Mathf.PI * settings.densityCheckRadius * settings.densityCheckRadius));
            settings.maxSpawnsPerFrame = Mathf.Max(1, profilingSettings.AsteroidCount);
            settings.densityCheckInterval = Mathf.Max(0.05f, settings.densityCheckInterval);
            CacheSettings();
        }
    }
}

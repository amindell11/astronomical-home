using System;
using Unity.Profiling;
using Cat = Diagnostics.Performance.FrameTimingAccumulator.Category;

namespace Diagnostics.Performance
{
    public static class LatencyProfilingMarkers
    {
        public const string AIUpdateName = "LatencyProfiling.AIUpdate";
        public const string ScoutScanName = "LatencyProfiling.ScoutScan";
        public const string ProjectileFireName = "LatencyProfiling.ProjectileFire";
        public const string ProjectileUpdateName = "LatencyProfiling.ProjectileUpdate";
        public const string ObjectiveTrackerName = "LatencyProfiling.ObjectiveTracker";
        public const string AsteroidSpawnName = "LatencyProfiling.AsteroidSpawn";
        public const string AsteroidUpdateName = "LatencyProfiling.AsteroidUpdate";

        public static readonly ProfilerMarker AIUpdate = new(AIUpdateName);
        public static readonly ProfilerMarker ScoutScan = new(ScoutScanName);
        public static readonly ProfilerMarker ProjectileFire = new(ProjectileFireName);
        public static readonly ProfilerMarker ProjectileUpdate = new(ProjectileUpdateName);
        public static readonly ProfilerMarker ObjectiveTracker = new(ObjectiveTrackerName);
        public static readonly ProfilerMarker AsteroidSpawn = new(AsteroidSpawnName);
        public static readonly ProfilerMarker AsteroidUpdate = new(AsteroidUpdateName);

        /// <summary>
        /// Returns an IDisposable scope that starts both the ProfilerMarker and
        /// the Stopwatch accumulator, and stops both on dispose.
        /// Use: using (LatencyProfilingMarkers.Measure(Cat.AIUpdate, AIUpdate)) { ... }
        /// </summary>
        public static TimedScope Measure(Cat category, ProfilerMarker marker)
        {
            return new TimedScope(category, marker);
        }

        public readonly struct TimedScope : IDisposable
        {
            private readonly ProfilerMarker.AutoScope profilerScope;
            private readonly Cat category;

            public TimedScope(Cat category, ProfilerMarker marker)
            {
                this.category = category;
                FrameTimingAccumulator.Begin(category);
                profilerScope = marker.Auto();
            }

            public void Dispose()
            {
                profilerScope.Dispose();
                FrameTimingAccumulator.End();
            }
        }
    }
}

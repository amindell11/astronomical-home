using Unity.Profiling;

namespace Diagnostics.Performance
{
    public static class LatencyProfilingMarkers
    {
        public const string AIUpdateName = "LatencyProfiling.AIUpdate";
        public const string ProjectileFireName = "LatencyProfiling.ProjectileFire";
        public const string ProjectileUpdateName = "LatencyProfiling.ProjectileUpdate";
        public const string ObjectiveTrackerName = "LatencyProfiling.ObjectiveTracker";
        public const string AsteroidSpawnName = "LatencyProfiling.AsteroidSpawn";

        public static readonly ProfilerMarker AIUpdate = new(AIUpdateName);
        public static readonly ProfilerMarker ProjectileFire = new(ProjectileFireName);
        public static readonly ProfilerMarker ProjectileUpdate = new(ProjectileUpdateName);
        public static readonly ProfilerMarker ObjectiveTracker = new(ObjectiveTrackerName);
        public static readonly ProfilerMarker AsteroidSpawn = new(AsteroidSpawnName);
    }
}

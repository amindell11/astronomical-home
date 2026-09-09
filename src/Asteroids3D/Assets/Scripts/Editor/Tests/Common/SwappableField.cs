using AI.Scanning;
using UnityEngine;

namespace Tests.Common
{
    /// <summary>Test-side obstacle source whose inner field a test swaps mid-run; ships are wired to this once and never re-wired.</summary>
    public sealed class SwappableField : IObstacleField
    {
        public IObstacleField Inner;

        public int QueryObstacles(Vector2 centerPlane, float halfExtent, DetectedObstacle[] buffer) =>
            Inner?.QueryObstacles(centerPlane, halfExtent, buffer) ?? 0;
    }
}

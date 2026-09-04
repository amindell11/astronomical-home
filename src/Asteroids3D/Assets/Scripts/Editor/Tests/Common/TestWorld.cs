using AI.Scanning;
using Game.Services;
using Ships;
using UnityEngine;
using Ships.Registry;

namespace Tests.Common
{
    /// <summary>Builds a test <see cref="WorldHandle"/>: zero offset, a stub registry unless one is given, and an optional obstacle field.</summary>
    public static class TestWorld
    {
        public static WorldHandle On(IShipRegistry registry = null, IObstacleField field = null)
        {
            return new WorldHandle(Vector2.zero, registry ?? new StubShipRegistry(), field);
        }

        /// <summary>Test-side obstacle source whose inner field a test swaps mid-run; the handle itself stays immutable.</summary>
        public sealed class SwappableField : IObstacleField
        {
            public IObstacleField Inner;

            public int QueryObstacles(Vector2 centerPlane, float halfExtent, DetectedObstacle[] buffer) =>
                Inner?.QueryObstacles(centerPlane, halfExtent, buffer) ?? 0;
        }
    }
}

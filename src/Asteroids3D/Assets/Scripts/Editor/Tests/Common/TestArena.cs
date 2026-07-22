using Game.Services;
using Ships;
using UnityEngine;

namespace Tests.Common
{
    /// <summary>Builds a test <see cref="ArenaContext"/> on a caller-owned host GameObject.</summary>
    public static class TestArena
    {
        public static ArenaContext On(GameObject host, IShipRegistry registry = null)
        {
            return new ArenaContext(Vector2.zero, registry ?? new StubShipRegistry());
        }
    }
}

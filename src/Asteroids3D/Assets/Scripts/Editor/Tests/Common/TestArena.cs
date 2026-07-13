using Game.Services;
using Movement.MPC.Field;
using Ships;
using UnityEngine;

namespace Tests.Common
{
    /// <summary>Builds a test <see cref="ArenaContext"/> on a caller-owned host GameObject so the NavField sibling is cleaned up with the fixture.</summary>
    public static class TestArena
    {
        public static ArenaContext On(GameObject host, IShipRegistry registry = null)
        {
            var navField = host.GetComponent<NavFieldService>();
            if (!navField) navField = host.AddComponent<NavFieldService>();
            return new ArenaContext(Vector2.zero, registry ?? new StubShipRegistry(), navField);
        }
    }
}

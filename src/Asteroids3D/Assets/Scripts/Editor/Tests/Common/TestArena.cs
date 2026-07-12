using Game.Services;
using Movement.MPC.Field;
using Ships;
using UnityEngine;

namespace Tests.Common
{
    /// <summary>
    /// Builds an <see cref="ArenaContext"/> for tests: attaches the required <see cref="NavFieldService"/>
    /// sibling to a caller-owned host GameObject (so it is cleaned up with the rest of the fixture) and
    /// wraps a registry (a <see cref="StubShipRegistry"/> by default). The obstacle slot starts null; a
    /// test sets <c>arena.ObstacleField</c> to inject a stub field.
    /// </summary>
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

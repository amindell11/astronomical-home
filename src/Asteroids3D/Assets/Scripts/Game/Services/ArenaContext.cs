using System;
using AI.Scanning;
using Movement.MPC.Field;
using Ships;
using UnityEngine;

namespace Game.Services
{
    /// <summary>
    /// Per-arena world-frame handle: the single injection surface for the world providers an AI ship
    /// reads (obstacle field, cost-to-go field, ship registry) plus the arena's in-plane offset. A
    /// plain handle with no lifecycle — consumers dereference its provider slots each frame, so a
    /// register-later field (set during sector setup, after ships are wired) is picked up live.
    /// </summary>
    public class ArenaContext
    {
        public Vector2 Offset { get; }
        public IShipRegistry Registry { get; }
        public NavFieldService NavField { get; }

        /// <summary>Live obstacle source; null between sectors means "sense zero static obstacles".</summary>
        public IObstacleField ObstacleField { get; set; }

        public ArenaContext(Vector2 offset, IShipRegistry registry, NavFieldService navField)
        {
            Offset = offset;
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            NavField = navField ? navField : throw new ArgumentNullException(nameof(navField));
        }
    }
}

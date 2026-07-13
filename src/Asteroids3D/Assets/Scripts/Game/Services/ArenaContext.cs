using System;
using AI.Scanning;
using Movement.MPC.Field;
using Ships;
using UnityEngine;

namespace Game.Services
{
    /// <summary>Per-arena handle bundling the world providers an AI ship reads plus the arena's in-plane offset; slots are dereferenced each frame, so a field registered after ships are wired is picked up live.</summary>
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

        /// <summary>World position of an AUTHORED plane-space point. Live entity positions already carry the offset and round-trip through <see cref="GamePlane"/> instead.</summary>
        public Vector3 Place(Vector2 planePoint) => GamePlane.PlanePointToWorld(planePoint + Offset);
    }
}

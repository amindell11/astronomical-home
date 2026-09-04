using System;
using AI.Scanning;
using Ships;
using UnityEngine;
using Ships.Registry;

namespace Game.Services
{
    /// <summary>
    /// The immutable per-world-load handle an AI ship reads its world through: the in-plane offset,
    /// the ship registry and the obstacle field (null for a world with no rocks). Built only at the
    /// composition root — the session host per compose/load, a harness composition per field — and
    /// handed down the spawn call, so a ship wired to one world can never observe another's field.
    /// </summary>
    public sealed class WorldHandle
    {
        public Vector2 Offset { get; }
        public IShipRegistry Registry { get; }
        public IObstacleField ObstacleField { get; }

        public WorldHandle(Vector2 offset, IShipRegistry registry, IObstacleField obstacleField)
        {
            Offset = offset;
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            ObstacleField = obstacleField;
        }

        /// <summary>World position of an AUTHORED plane-space point. Live entity positions already carry the offset and round-trip through <see cref="GamePlane"/> instead.</summary>
        public Vector3 Place(Vector2 planePoint) => GamePlane.PlanePointToWorld(planePoint + Offset);
    }
}

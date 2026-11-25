using System.Collections.Generic;
using UnityEngine;

namespace Asteroids.Fields.Core
{
    /// <summary>
    /// Baseline LUT + sparse override overlay = what actually exists in the
    /// field right now. Headless: chunk contents are computed here so
    /// determinism and persistence are unit-testable without physics.
    /// </summary>
    public class AsteroidFieldModel
    {
        public AsteroidFieldLayout Layout { get; }
        public AsteroidFieldOverlay Overlay { get; }

        private int nextAuthoredIndex;

        public AsteroidFieldModel(AsteroidFieldLayout layout, AsteroidFieldOverlay overlay = null)
        {
            Layout = layout;
            Overlay = overlay ?? new AsteroidFieldOverlay();
        }

        public AsteroidId NextAuthoredId() => AsteroidId.CreateAuthored(nextAuthoredIndex++);

        /// <summary>
        /// Appends everything that should exist in a chunk: baseline asteroids
        /// minus tombstones with recorded damage applied, plus authored
        /// entries (fragments) homed inside the chunk.
        /// </summary>
        public void GetChunkContents(Vector2Int chunk, List<FieldAsteroidSpec> results)
        {
            var start = results.Count;
            Layout.GenerateCell(chunk.x, chunk.y, results);

            // Filter tombstones in place and apply damage overlay.
            var write = start;
            for (var read = start; read < results.Count; read++)
            {
                var spec = results[read];
                if (Overlay.IsTombstoned(spec.Id)) continue;
                if (Overlay.DamagedHealth.TryGetValue(spec.Id, out var health))
                    spec.HealthFraction = health;
                results[write++] = spec;
            }
            results.RemoveRange(write, results.Count - write);

            // Authored entries are sparse; a linear scan is fine.
            foreach (var entry in Overlay.Authored.Values)
            {
                if (Layout.CellOf(entry.PlanePosition) != chunk) continue;
                results.Add(new FieldAsteroidSpec
                {
                    Id = entry.Id,
                    PlanePosition = entry.PlanePosition,
                    Rotation = entry.Rotation,
                    MeshIndex = entry.MeshIndex,
                    Mass = entry.Mass,
                    Scale = entry.Scale,
                    PlaneVelocity = Vector2.zero,
                    AngularVelocity = Vector3.zero,
                    HealthFraction = entry.HealthFraction
                });
            }
        }
    }
}

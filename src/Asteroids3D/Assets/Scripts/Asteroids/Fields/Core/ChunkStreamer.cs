using System.Collections.Generic;
using UnityEngine;

namespace Asteroids.Fields.Core
{
    /// <summary>
    /// Pure load/unload set logic for the unified cell-equals-chunk grid.
    /// Chunks load when their center enters the load radius and only unload
    /// past the (wider) unload radius — the hysteresis band prevents boundary
    /// thrash and pushes the fragment-freeze case out of view.
    /// </summary>
    public class ChunkStreamer
    {
        private readonly float cellSize;
        private readonly float loadRadius;
        private readonly float unloadRadius;
        private readonly HashSet<Vector2Int> loaded = new();

        public IReadOnlyCollection<Vector2Int> Loaded => loaded;

        public ChunkStreamer(float cellSize, float loadRadius, float unloadRadius)
        {
            this.cellSize = cellSize;
            this.loadRadius = loadRadius;
            this.unloadRadius = Mathf.Max(unloadRadius, loadRadius);
        }

        public bool IsLoaded(Vector2Int chunk) => loaded.Contains(chunk);

        /// <summary>
        /// Advances the loaded set toward the anchor position, appending newly
        /// loaded chunks to <paramref name="toLoad"/> and newly unloaded ones
        /// to <paramref name="toUnload"/>. The caller executes the actual
        /// spawn/despawn work.
        /// </summary>
        public void Update(Vector2 anchorPlanePos, List<Vector2Int> toLoad, List<Vector2Int> toUnload)
        {
            var minX = Mathf.FloorToInt((anchorPlanePos.x - loadRadius) / cellSize);
            var maxX = Mathf.FloorToInt((anchorPlanePos.x + loadRadius) / cellSize);
            var minY = Mathf.FloorToInt((anchorPlanePos.y - loadRadius) / cellSize);
            var maxY = Mathf.FloorToInt((anchorPlanePos.y + loadRadius) / cellSize);

            for (var cy = minY; cy <= maxY; cy++)
            for (var cx = minX; cx <= maxX; cx++)
            {
                var chunk = new Vector2Int(cx, cy);
                if (loaded.Contains(chunk)) continue;
                if (DistanceToChunkCenter(chunk, anchorPlanePos) > loadRadius) continue;
                loaded.Add(chunk);
                toLoad.Add(chunk);
            }

            foreach (var chunk in loaded)
            {
                if (DistanceToChunkCenter(chunk, anchorPlanePos) > unloadRadius)
                    toUnload.Add(chunk);
            }
            foreach (var chunk in toUnload) loaded.Remove(chunk);
        }

        public void Reset() => loaded.Clear();

        private float DistanceToChunkCenter(Vector2Int chunk, Vector2 pos)
        {
            var center = new Vector2((chunk.x + 0.5f) * cellSize, (chunk.y + 0.5f) * cellSize);
            return Vector2.Distance(center, pos);
        }
    }
}

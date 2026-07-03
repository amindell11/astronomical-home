#if UNITY_EDITOR
using Asteroids.Fields.Core;
using Game;
using UnityEditor;
using UnityEngine;

namespace Asteroids.Fields
{
    public partial class UpdatingAsteroidField
    {
        private const float HeatmapAlpha = 0.18f;
        private const int MaxGizmoCellsPerAxis = 64;

        // Edit-mode preview of the exact runtime layout (same seed + params via
        // the same core code), so density can be tuned without entering play.
        private AsteroidFieldLayout previewLayout;
        private int previewSettingsVersion = -1;
        private int previewSeed;

        protected override void OnDrawGizmosSelected()
        {
            // Field boundary from the base class.
            base.OnDrawGizmosSelected();
            if (!settings) return;

            var layout = ActiveLayout();
            if (layout == null) return;

            var originPlane = initialized ? fieldOriginPlane : WorldToPlaneSafe(transform.position);
            var anchorWorld = Application.isPlaying && initialized && CurrentAnchorPos != null
                ? CurrentAnchorPos()
                : transform.position;
            var anchorPlane = WorldToPlaneSafe(anchorWorld) - originPlane;

            if (drawNoiseHeatmap) DrawNoiseHeatmap(layout, originPlane, anchorPlane);
            if (drawChunkGizmos) DrawChunkGrid(originPlane, anchorPlane);

            // Streaming radii around the anchor.
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(anchorWorld, settings.loadRadius);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(anchorWorld, settings.UnloadRadius);
            Handles.color = Color.white;
            Handles.Label(anchorWorld + Vector3.forward * (settings.loadRadius + 2f), "Load");
            Handles.Label(anchorWorld + Vector3.forward * (settings.UnloadRadius + 2f), "Unload");
        }

        /// <summary>Runtime layout when playing; otherwise a settings-synced preview.</summary>
        private AsteroidFieldLayout ActiveLayout()
        {
            if (initialized && Model != null) return Model.Layout;

            if (previewLayout == null || previewSettingsVersion != settings.Version || previewSeed != seed)
            {
                previewLayout = new AsteroidFieldLayout(seed, new FieldGenerationParams
                {
                    CellSize = settings.chunkSize,
                    AverageAsteroidsPerCell = settings.averageAsteroidsPerCell,
                    NoiseFrequency = settings.noiseFrequency,
                    DensityMultiplierRange = settings.densityMultiplierRange,
                    FieldRadius = settings.fieldRadius,
                    // Attribute inputs don't influence density/counts.
                    MeshVolumes = System.Array.Empty<float>()
                });
                previewSettingsVersion = settings.Version;
                previewSeed = seed;
            }
            return previewLayout;
        }

        private void DrawNoiseHeatmap(AsteroidFieldLayout layout, Vector2 originPlane, Vector2 anchorPlane)
        {
            var cell = settings.chunkSize;
            if (cell <= 0f) return;

            var radius = Mathf.Min(settings.fieldRadius, settings.UnloadRadius * 2f);
            ForEachCellInRange(anchorPlane, radius, cell, (cx, cy, center) =>
            {
                if (center.magnitude > settings.fieldRadius) return;
                var multiplier = layout.DensityMultiplier(cx, cy);
                var t = Mathf.InverseLerp(settings.densityMultiplierRange.x, settings.densityMultiplierRange.y, multiplier);
                var color = Color.Lerp(new Color(0.1f, 0.3f, 1f), new Color(1f, 0.25f, 0.1f), t);
                color.a = HeatmapAlpha;

                Handles.DrawSolidRectangleWithOutline(CellCorners(cx, cy, cell, originPlane), color, Color.clear);
            });
        }

        private void DrawChunkGrid(Vector2 originPlane, Vector2 anchorPlane)
        {
            var cell = settings.chunkSize;
            if (cell <= 0f) return;

            var dimGrid = new Color(1f, 1f, 1f, 0.08f);
            var loadedColor = new Color(0.2f, 1f, 0.2f, 0.9f);
            var queuedColor = new Color(1f, 0.9f, 0.2f, 0.9f);

            var radius = settings.UnloadRadius + cell;
            ForEachCellInRange(anchorPlane, radius, cell, (cx, cy, _) =>
            {
                var chunk = new Vector2Int(cx, cy);
                var color = dimGrid;
                if (Application.isPlaying && initialized && streamer != null)
                {
                    if (queuedChunks.Contains(chunk)) color = queuedColor;
                    else if (streamer.IsLoaded(chunk)) color = loadedColor;
                }

                Handles.DrawSolidRectangleWithOutline(CellCorners(cx, cy, cell, originPlane), Color.clear, color);
            });
        }

        private static void ForEachCellInRange(Vector2 anchorPlane, float radius, float cell,
            System.Action<int, int, Vector2> drawCell)
        {
            var minX = Mathf.FloorToInt((anchorPlane.x - radius) / cell);
            var maxX = Mathf.FloorToInt((anchorPlane.x + radius) / cell);
            var minY = Mathf.FloorToInt((anchorPlane.y - radius) / cell);
            var maxY = Mathf.FloorToInt((anchorPlane.y + radius) / cell);
            if (maxX - minX > MaxGizmoCellsPerAxis || maxY - minY > MaxGizmoCellsPerAxis) return;

            for (var cy = minY; cy <= maxY; cy++)
            for (var cx = minX; cx <= maxX; cx++)
            {
                var center = new Vector2((cx + 0.5f) * cell, (cy + 0.5f) * cell);
                if (Vector2.Distance(center, anchorPlane) > radius) continue;
                drawCell(cx, cy, center);
            }
        }

        private static Vector3[] CellCorners(int cx, int cy, float cell, Vector2 originPlane)
        {
            var min = new Vector2(cx * cell, cy * cell) + originPlane;
            return new[]
            {
                PlanePointToWorldSafe(min),
                PlanePointToWorldSafe(min + new Vector2(cell, 0f)),
                PlanePointToWorldSafe(min + new Vector2(cell, cell)),
                PlanePointToWorldSafe(min + new Vector2(0f, cell))
            };
        }

        // GamePlane is only configured during bootstrap; fall back to the XZ
        // plane in edit mode (matches the game's PlaneAxis.Y convention).
        private static Vector2 WorldToPlaneSafe(Vector3 world) =>
            GamePlane.IsConfigured ? GamePlane.WorldPointToPlane(world) : new Vector2(world.x, world.z);

        private static Vector3 PlanePointToWorldSafe(Vector2 plane) =>
            GamePlane.IsConfigured ? GamePlane.PlanePointToWorld(plane) : new Vector3(plane.x, 0f, plane.y);
    }
}
#endif

using System.Runtime.CompilerServices;
using Asteroids.Fields.Core;
using Game;
using UnityEditor;
using UnityEngine;

namespace Asteroids.Fields
{
    internal static class AsteroidFieldGizmos
    {
        private const float HeatmapAlpha = 0.18f;
        private const int MaxGizmoCellsPerAxis = 64;

        // Edit-mode preview of the exact runtime layout (same seed + params via the same core code).
        private sealed class PreviewState
        {
            public AsteroidFieldLayout Layout;
            public int SettingsVersion = -1;
            public int Seed;
        }

        private static readonly ConditionalWeakTable<UpdatingAsteroidField, PreviewState> Previews = new();

        [DrawGizmo(GizmoType.Selected, typeof(AsteroidField))]
        private static void Draw(AsteroidField field, GizmoType gizmoType)
        {
            if (!field.settings) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(field.transform.position, field.settings.fieldRadius);

            if (field is UpdatingAsteroidField updating) DrawStreaming(updating);
        }

        private static void DrawStreaming(UpdatingAsteroidField field)
        {
            var settings = field.settings;
            var layout = ActiveLayout(field);
            if (layout == null) return;

            var originPlane = field.initialized ? field.fieldOriginPlane : WorldToPlaneSafe(field.transform.position);
            var anchorWorld = Application.isPlaying && field.initialized && field.CurrentAnchorPos != null
                ? field.CurrentAnchorPos()
                : field.transform.position;
            var anchorPlane = WorldToPlaneSafe(anchorWorld) - originPlane;

            if (field.drawNoiseHeatmap) DrawNoiseHeatmap(settings, layout, originPlane, anchorPlane);
            if (field.drawChunkGizmos) DrawChunkGrid(field, originPlane, anchorPlane);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(anchorWorld, settings.loadRadius);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(anchorWorld, settings.UnloadRadius);
            Handles.color = Color.white;
            Handles.Label(anchorWorld + Vector3.forward * (settings.loadRadius + 2f), "Load");
            Handles.Label(anchorWorld + Vector3.forward * (settings.UnloadRadius + 2f), "Unload");
        }

        /// <summary>Runtime layout when playing; otherwise a settings-synced preview.</summary>
        private static AsteroidFieldLayout ActiveLayout(UpdatingAsteroidField field)
        {
            if (field.initialized && field.Model != null) return field.Model.Layout;

            var settings = field.settings;
            var preview = Previews.GetValue(field, _ => new PreviewState());
            if (preview.Layout == null || preview.SettingsVersion != settings.Version || preview.Seed != field.seed)
            {
                // Attribute inputs (mesh volumes etc.) don't influence density, so the settings-built params alone give an exact preview.
                preview.Layout = new AsteroidFieldLayout(field.seed, settings.BuildGenerationParams());
                preview.SettingsVersion = settings.Version;
                preview.Seed = field.seed;
            }
            return preview.Layout;
        }

        private static void DrawNoiseHeatmap(AsteroidFieldSettings settings, AsteroidFieldLayout layout,
            Vector2 originPlane, Vector2 anchorPlane)
        {
            var cell = settings.chunkSize;
            if (cell <= 0f) return;

            var radius = Mathf.Min(settings.fieldRadius, settings.UnloadRadius * 2f);
            ForEachCellInRange(anchorPlane, radius, cell, (cx, cy, center) =>
            {
                if (center.magnitude > settings.fieldRadius) return;
                // Pre-rejection density: the heatmap (like CountForCell) shows authored density and
                // deliberately overstates on-screen counts when overlap rejection is active — do not "fix" this.
                var multiplier = layout.DensityMultiplier(cx, cy);
                Color color;
                if (multiplier <= 0f)
                {
                    // True void (below the density floor): near-black.
                    color = new Color(0f, 0f, 0f, HeatmapAlpha * 0.75f);
                }
                else
                {
                    var t = Mathf.InverseLerp(settings.densityMultiplierRange.x, settings.densityMultiplierRange.y, multiplier);
                    color = Color.Lerp(new Color(0.1f, 0.3f, 1f), new Color(1f, 0.25f, 0.1f), t);
                    color.a = HeatmapAlpha;
                }

                Handles.DrawSolidRectangleWithOutline(CellCorners(cx, cy, cell, originPlane), color, Color.clear);
            });
        }

        private static void DrawChunkGrid(UpdatingAsteroidField field, Vector2 originPlane, Vector2 anchorPlane)
        {
            var cell = field.settings.chunkSize;
            if (cell <= 0f) return;

            var dimGrid = new Color(1f, 1f, 1f, 0.08f);
            var loadedColor = new Color(0.2f, 1f, 0.2f, 0.9f);
            var queuedColor = new Color(1f, 0.9f, 0.2f, 0.9f);

            var radius = field.settings.UnloadRadius + cell;
            ForEachCellInRange(anchorPlane, radius, cell, (cx, cy, _) =>
            {
                var chunk = new Vector2Int(cx, cy);
                var color = dimGrid;
                if (Application.isPlaying && field.initialized && field.streamer != null)
                {
                    if (field.queuedChunks.Contains(chunk)) color = queuedColor;
                    else if (field.streamer.IsLoaded(chunk)) color = loadedColor;
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

        private static Vector2 WorldToPlaneSafe(Vector3 world) => GamePlane.WorldPointToPlane(world);

        private static Vector3 PlanePointToWorldSafe(Vector2 plane) => GamePlane.PlanePointToWorld(plane);
    }
}

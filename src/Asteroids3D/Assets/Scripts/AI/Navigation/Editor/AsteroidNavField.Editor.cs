#if UNITY_EDITOR
using Game;
using UnityEngine;

namespace AI.Planning
{
    public partial class AsteroidNavField
    {
        [Header("Gizmo")]
        [SerializeField] private bool drawGizmos = true;
        [Tooltip("Show grid bounds wire box. Cheap.")]
        [SerializeField] private bool drawGridBounds = true;
        [Tooltip("Show blocked cells. Drawn only when this GameObject is selected (avoids per-frame cost).")]
        [SerializeField] private bool drawBlockedCells = true;
        [Tooltip("Show cost-to-go heatmap. Drawn only when this GameObject is selected (~2500 sphere draws).")]
        [SerializeField] private bool drawCostHeatmap;
        [Tooltip("Show source cell with a green sphere. Drawn only when this GameObject is selected.")]
        [SerializeField] private bool drawSource = true;

        // Always-on, cheap. Just the grid bounds box. Runs every Scene-view frame.
        private void OnDrawGizmos()
        {
            if (!drawGizmos || !drawGridBounds) return;

            var anchorPos = anchor ? anchor.position : transform.position;

            if (!GamePlane.IsConfigured)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(anchorPos, 5f);
                return;
            }

            var anchorPlane = GamePlane.WorldPointToPlane(anchorPos);
            var halfExtent = gridSize * cellSize * 0.5f;
            var originPlane = anchorPlane - new Vector2(halfExtent, halfExtent);
            var size = gridSize * cellSize;
            var c0 = GamePlane.PlanePointToWorld(originPlane);
            var c1 = GamePlane.PlanePointToWorld(originPlane + new Vector2(size, 0));
            var c2 = GamePlane.PlanePointToWorld(originPlane + new Vector2(size, size));
            var c3 = GamePlane.PlanePointToWorld(originPlane + new Vector2(0, size));
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.5f);
            Gizmos.DrawLine(c0, c1);
            Gizmos.DrawLine(c1, c2);
            Gizmos.DrawLine(c2, c3);
            Gizmos.DrawLine(c3, c0);
        }

        // Only fires when this GO is selected — guarded against per-frame stalls from
        // 2500-cell sphere draws.
        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            if (!GamePlane.IsConfigured) return;
            if (!drawBlockedCells && !drawCostHeatmap && !drawSource) return;

            foreach (var kvp in CachedFields())
            {
                var field = kvp.Value;
                if (field == null) continue;
                DrawField(field);
            }
        }

        private void DrawField(NavField field)
        {
            // Single pass: find max cost and gather source cell while iterating cells once.
            var maxCost = 1f;
            var sourceX = -1;
            var sourceY = -1;
            var sphereRadius = field.CellSize * 0.35f;

            if (drawCostHeatmap || drawSource)
            {
                for (var y = 0; y < field.GridSize; y++)
                {
                    for (var x = 0; x < field.GridSize; x++)
                    {
                        var c = field.CostToGoAt(x, y);
                        if (float.IsPositiveInfinity(c)) continue;
                        if (c > maxCost) maxCost = c;
                        if (sourceX < 0 && c == 0f) { sourceX = x; sourceY = y; }
                    }
                }
            }

            for (var y = 0; y < field.GridSize; y++)
            {
                for (var x = 0; x < field.GridSize; x++)
                {
                    var blocked = field.IsCellBlocked(x, y);
                    if (blocked && drawBlockedCells)
                    {
                        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
                        Gizmos.DrawSphere(GamePlane.PlanePointToWorld(field.CellCenterAt(x, y)), sphereRadius);
                    }
                    else if (!blocked && drawCostHeatmap)
                    {
                        var c = field.CostToGoAt(x, y);
                        if (float.IsPositiveInfinity(c)) continue;
                        var t = Mathf.Clamp01(c / maxCost);
                        Gizmos.color = new Color(t, 1f - t, 0.2f, 0.4f);
                        Gizmos.DrawSphere(GamePlane.PlanePointToWorld(field.CellCenterAt(x, y)), sphereRadius * 0.5f);
                    }
                }
            }

            if (drawSource && sourceX >= 0)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(GamePlane.PlanePointToWorld(field.CellCenterAt(sourceX, sourceY)), field.CellSize * 0.5f);
            }
        }
    }
}
#endif

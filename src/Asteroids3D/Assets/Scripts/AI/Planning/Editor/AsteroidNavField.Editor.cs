#if UNITY_EDITOR
using Game;
using UnityEngine;

namespace AI.Planning
{
    public partial class AsteroidNavField
    {
        [Header("Gizmo")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private bool drawBlockedCells = true;
        [SerializeField] private bool drawCostHeatmap = false;
        [SerializeField] private bool drawSource = true;
        [SerializeField] private bool drawGridBounds = true;

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            if (!anchor) return;
            if (!GamePlane.IsConfigured) return;

            var anchorPlane = GamePlane.WorldPointToPlane(anchor.position);
            var halfExtent = gridSize * cellSize * 0.5f;
            var originPlane = anchorPlane - new Vector2(halfExtent, halfExtent);

            if (drawGridBounds)
            {
                Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.5f);
                var size = gridSize * cellSize;
                var corners = new[]
                {
                    GamePlane.PlanePointToWorld(originPlane),
                    GamePlane.PlanePointToWorld(originPlane + new Vector2(size, 0)),
                    GamePlane.PlanePointToWorld(originPlane + new Vector2(size, size)),
                    GamePlane.PlanePointToWorld(originPlane + new Vector2(0, size)),
                };
                for (var i = 0; i < 4; i++)
                {
                    Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
                }
            }

            foreach (var kvp in CachedFields())
            {
                var field = kvp.Value;
                if (field == null) continue;
                DrawField(field);
            }
        }

        private void DrawField(NavField field)
        {
            var maxCost = 1f;
            if (drawCostHeatmap)
            {
                for (var y = 0; y < field.GridSize; y++)
                for (var x = 0; x < field.GridSize; x++)
                {
                    var c = field.CostToGoAt(x, y);
                    if (!float.IsPositiveInfinity(c) && c > maxCost) maxCost = c;
                }
            }

            var sphereRadius = field.CellSize * 0.35f;

            for (var y = 0; y < field.GridSize; y++)
            {
                for (var x = 0; x < field.GridSize; x++)
                {
                    var cellWorld = GamePlane.PlanePointToWorld(field.CellCenterAt(x, y));

                    if (drawBlockedCells && field.IsCellBlocked(x, y))
                    {
                        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
                        Gizmos.DrawSphere(cellWorld, sphereRadius);
                    }
                    else if (drawCostHeatmap)
                    {
                        var c = field.CostToGoAt(x, y);
                        if (float.IsPositiveInfinity(c)) continue;
                        var t = Mathf.Clamp01(c / maxCost);
                        Gizmos.color = new Color(t, 1f - t, 0.2f, 0.4f);
                        Gizmos.DrawSphere(cellWorld, sphereRadius * 0.5f);
                    }
                }
            }

            if (drawSource)
            {
                for (var y = 0; y < field.GridSize; y++)
                for (var x = 0; x < field.GridSize; x++)
                {
                    if (field.CostToGoAt(x, y) == 0f)
                    {
                        var srcWorld = GamePlane.PlanePointToWorld(field.CellCenterAt(x, y));
                        Gizmos.color = Color.green;
                        Gizmos.DrawSphere(srcWorld, field.CellSize * 0.5f);
                    }
                }
            }
        }
    }
}
#endif

#if UNITY_EDITOR
using Game;
using UnityEngine;

namespace Movement.MPC.Field
{
    /// <summary>
    /// Editor-only visualization for the terminal cost-to-go fields, following the MPC
    /// editor-partial pattern (Navigator.Editor). Select the runtime [NavFieldService]
    /// object and enable the toggle to see blocked cells (red) and a cost heatmap
    /// (green near the target, yellow/orange far).
    /// </summary>
    public partial class NavFieldService
    {
        [Header("Debug (editor only)")]
        [SerializeField] private bool drawFieldGizmos;

        private void OnDrawGizmos()
        {
            if (!drawFieldGizmos || !GamePlane.IsConfigured) return;

            foreach (var kvp in fields)
            {
                var field = kvp.Value.front;
                if (field == null || !field.HasSolution) continue;

                var n = field.GridSize;
                var cs = field.CellSize;
                var origin = field.Origin;
                var cost = field.CostToGo;
                var blocked = field.Blocked;

                // Normalize color scale to the grid diameter in steps.
                var maxSteps = n * 1.5f;
                for (var y = 0; y < n; y++)
                {
                    for (var x = 0; x < n; x++)
                    {
                        var idx = y * n + x;
                        var center = new Vector2(origin.x + (x + 0.5f) * cs, origin.y + (y + 0.5f) * cs);
                        var world = GamePlane.PlanePointToWorld(center);

                        if (blocked[idx] != 0)
                        {
                            Gizmos.color = new Color(1f, 0.15f, 0.1f, 0.35f);
                            Gizmos.DrawCube(world, Vector3.one * cs * 0.9f);
                            continue;
                        }

                        var c = cost[idx];
                        if (float.IsInfinity(c)) continue;
                        var t = Mathf.Clamp01(c / maxSteps);
                        Gizmos.color = Color.Lerp(
                            new Color(0.1f, 1f, 0.2f, 0.18f),
                            new Color(1f, 0.6f, 0f, 0.18f), t);
                        Gizmos.DrawCube(world, Vector3.one * cs * 0.8f);
                    }
                }
            }
        }
    }
}
#endif

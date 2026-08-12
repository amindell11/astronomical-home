using System.Runtime.CompilerServices;
using Game;
using Game.Diagnostics;
using Ships;
using UnityEditor;
using UnityEngine;

namespace Combat.Weapons
{
    /// <summary>Laser-bank heat as a filled bar off the ship's right flank, so it stays on the same side as the ship turns, plus the numeric readout.</summary>
    internal static class LasersGizmos
    {
        private const float FlankOffset = 1.5f;
        private const float BarLength = 1f;
        private const float BarWidth = 0.3f;
        private const float ScanSpacing = 0.02f;

        private static readonly Color Track = new(0.5f, 0.5f, 0.5f, 0.5f);

        private static readonly ConditionalWeakTable<Lasers, Ship> ParentShips = new();

        [DrawGizmo(GizmoType.Selected, typeof(Lasers))]
        private static void DrawHeatBar(Lasers lasers, GizmoType gizmoType)
        {
            if (!Application.isPlaying || !lasers.Heat) return;
            var ship = ParentShips.GetValue(lasers, l => l.GetComponentInParent<Ship>());
            if (!ship) return;

            var shipTransform = ship.transform;
            var foot = GamePlane.WorldPointToPlane(shipTransform.position + shipTransform.right * FlankOffset);
            var pct = lasers.Heat.HeatPct;

            DrawColumn(foot, BarLength, Track);
            if (pct > 0f) DrawColumn(foot, BarLength * pct, Color.Lerp(Color.cyan, Color.red, pct));

            ShipReadout.Draw(ship.Kinematics.pos, ShipReadoutRow.Heat,
                $"Heat: {lasers.Heat.CurrentHeat:F0}/{lasers.Heat.MaxHeat:F0}", Color.white);
        }

        private static void DrawColumn(Vector2 foot, float length, Color color)
        {
            if (length <= 0f) return;
            Gizmos.color = color;
            // Gizmos has no filled quad; approximate with in-plane scan lines.
            var steps = Mathf.Max(2, Mathf.CeilToInt(BarWidth / ScanSpacing));
            for (var i = 0; i <= steps; i++)
            {
                var x = foot.x - BarWidth * 0.5f + i / (float)steps * BarWidth;
                Gizmos.DrawLine(GamePlane.PlanePointToWorld(new Vector2(x, foot.y)),
                    GamePlane.PlanePointToWorld(new Vector2(x, foot.y + length)));
            }
        }
    }
}

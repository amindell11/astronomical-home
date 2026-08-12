using System.Runtime.CompilerServices;
using Damage;
using Game;
using Game.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace Ships.Damage
{
    /// <summary>Filled shield/health bars plus their numeric readout, in plane space. The serialized maxima carry the bars in edit mode, where <see cref="DamageController"/>'s pools do not exist yet.</summary>
    internal static class DamageControllerGizmos
    {
        private const float BaseOffset = 2f;
        private const float BarWidth = 3.5f;
        private const float BarHeight = 0.25f;
        // Filled tracks abut at the painter-era spacing, reading as one two-tone block.
        private const float BarSpacing = BarHeight * 1.6f;
        private const float ScanSpacing = 0.02f;

        private static readonly ConditionalWeakTable<DamageController, Ship> ParentShips = new();

        [DrawGizmo(GizmoType.Selected, typeof(DamageController))]
        private static void DrawHealthBars(DamageController damage, GizmoType gizmoType)
        {
            var ship = ParentShips.GetValue(damage, d => d.GetComponentInParent<Ship>());
            if (!ship) return;

            var shieldBar = GamePlane.WorldPointToPlane(damage.transform.position) + new Vector2(0f, BaseOffset);
            DrawBar(shieldBar, Pct(damage.Shield, damage.maxShield), Color.gray, Color.cyan);
            DrawBar(shieldBar + new Vector2(0f, BarSpacing), Pct(damage.Health, damage.maxHealth),
                Color.red, Color.green);

            var subject = Application.isPlaying
                ? ship.Kinematics.pos
                : GamePlane.WorldPointToPlane(ship.transform.position);
            ShipReadout.Draw(subject, ShipReadoutRow.Shield,
                $"Shield: {Current(damage.Shield, damage.maxShield):F1}/{damage.maxShield:F1}", Color.white);
            ShipReadout.Draw(subject, ShipReadoutRow.Health,
                $"Health: {Current(damage.Health, damage.maxHealth):F1}/{damage.maxHealth:F1}", Color.white);
        }

        // The pools are built in Awake; before that the serialized maximum is the only truth and reads full.
        private static float Pct(Resource pool, float max) => pool != null ? pool.Pct : max > 0f ? 1f : 0f;

        private static float Current(Resource pool, float max) => pool != null ? pool.CurrentValue : max;

        private static void DrawBar(Vector2 center, float pct, Color track, Color fill)
        {
            DrawFilledRect(center, BarWidth, track);
            if (pct <= 0f) return;
            var fillWidth = BarWidth * pct;
            DrawFilledRect(center - new Vector2((BarWidth - fillWidth) * 0.5f, 0f), fillWidth, fill);
        }

        private static void DrawFilledRect(Vector2 center, float width, Color color)
        {
            Gizmos.color = color;
            // Gizmos has no filled quad; approximate with in-plane scan lines.
            var steps = Mathf.Max(2, Mathf.CeilToInt(BarHeight / ScanSpacing));
            for (var i = 0; i <= steps; i++)
            {
                var y = center.y - BarHeight * 0.5f + i / (float)steps * BarHeight;
                Gizmos.DrawLine(GamePlane.PlanePointToWorld(new Vector2(center.x - width * 0.5f, y)),
                    GamePlane.PlanePointToWorld(new Vector2(center.x + width * 0.5f, y)));
            }
        }
    }
}

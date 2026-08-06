using System.Collections.Generic;
using Ships;
using Ships.Damage;
using UnityEngine;

namespace Game.Diagnostics
{
    /// <summary>Ship survivability in plane-space: stacked shield and health bars (track rect plus a left-anchored fill rect) above the hull, with numeric current/max labels. Damage controllers are cached at construction.</summary>
    public sealed class DamageBarsPainter : IDiagnosticPainter
    {
        private const float BaseOffset = 2f;
        private const float BarSpacing = 0.25f;
        private const float TextSpacing = 0.75f;
        private const float BarWidth = 3.5f;
        private const float BarHeight = 0.25f;

        private readonly List<DamageController> hulls = new();

        public DamageBarsPainter(Ship a, Ship b)
        {
            Cache(a);
            Cache(b);
        }

        public string Name => DiagnosticPainters.DamageBars;

        public void Paint(IDiagnosticCanvas canvas)
        {
            foreach (var hull in hulls) Draw(canvas, hull);
        }

        private void Cache(Ship ship)
        {
            if (!ship) return;
            var hull = ship.GetComponentInChildren<DamageController>();
            if (hull) hulls.Add(hull);
        }

        public static void Draw(IDiagnosticCanvas canvas, DamageController damage)
        {
            var shieldBar = GamePlane.WorldPointToPlane(damage.transform.position) + new Vector2(0f, BaseOffset);
            var healthBar = shieldBar + new Vector2(0f, BarSpacing);

            DrawBar(canvas, shieldBar, damage.maxShield > 0f ? damage.Shield.Pct : 0f, Color.gray, Color.cyan);
            DrawBar(canvas, healthBar, damage.maxHealth > 0f ? damage.Health.Pct : 0f, Color.red, Color.green);

            var shieldText = healthBar + new Vector2(0f, BarSpacing * 2f + TextSpacing * 2f);
            canvas.Label(shieldText, $"Shield: {damage.Shield.CurrentValue:F1}/{damage.maxShield:F1}", Color.white, 3f);
            canvas.Label(shieldText + new Vector2(0f, TextSpacing),
                $"Health: {damage.Health.CurrentValue:F1}/{damage.maxHealth:F1}", Color.white, 3f);
        }

        private static void DrawBar(IDiagnosticCanvas canvas, Vector2 center, float pct, Color track, Color fill)
        {
            canvas.Rect(center, new Vector2(BarWidth, BarHeight), track);
            if (pct <= 0f) return;
            var fillWidth = BarWidth * pct;
            canvas.Rect(center - new Vector2((BarWidth - fillWidth) * 0.5f, 0f), new Vector2(fillWidth, BarHeight),
                fill);
        }
    }
}

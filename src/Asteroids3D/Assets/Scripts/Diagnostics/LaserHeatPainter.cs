using System.Collections.Generic;
using Combat.Weapons;
using Ships;
using UnityEngine;

namespace Game.Diagnostics
{
    public sealed class LaserHeatPainter : IDiagnosticPainter
    {
        private const float BarOffsetX = 1.5f;
        private const float BarHeight = 1f;
        private const float LabelGap = 0.2f;

        private static readonly Color Track = new(0.5f, 0.5f, 0.5f, 0.5f);

        private readonly List<Lasers> banks = new();

        public LaserHeatPainter(Ship a, Ship b)
        {
            Cache(a);
            Cache(b);
        }

        public string Name => DiagnosticPainters.LaserHeat;

        public void Paint(IDiagnosticCanvas canvas)
        {
            foreach (var bank in banks) Draw(canvas, bank);
        }

        private void Cache(Ship ship)
        {
            if (!ship) return;
            var bank = ship.GetComponentInChildren<Lasers>();
            if (bank) banks.Add(bank);
        }

        public static void Draw(IDiagnosticCanvas canvas, Lasers lasers)
        {
            if (!lasers.Heat) return;
            var origin = GamePlane.WorldPointToPlane(lasers.transform.position) + new Vector2(BarOffsetX, 0f);
            var top = origin + new Vector2(0f, BarHeight);

            canvas.Label(top + new Vector2(0f, LabelGap),
                $"Heat: {lasers.Heat.CurrentHeat:F0}/{lasers.Heat.MaxHeat:F0}", Color.white, 3f);
            canvas.Line(origin, top, Track);

            var pct = lasers.Heat.HeatPct;
            if (pct <= 0f) return;
            canvas.Line(origin, Vector2.Lerp(origin, top, pct), Color.Lerp(Color.cyan, Color.red, pct));
        }
    }
}

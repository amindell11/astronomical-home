using System.Collections.Generic;
using AI;
using Combat;
using Ships;
using Ships.Command;
using UnityEngine;

namespace Game.Diagnostics
{
    /// <summary>Gunner firing solution in plane-space: the line and marker at the primary weapon's intercept point, the aim ray, and the fire-point line-of-sight test (green clear, red blocked). Gunners are cached at construction.</summary>
    public sealed class GunnerTargetingPainter : IDiagnosticPainter
    {
        private const float AimRayLength = 5f;
        private const float FirePointRadius = 0.5f;
        private static readonly Vector2 TargetMarkerSize = new(2f, 2f);

        private readonly List<Gunner> gunners = new();

        public GunnerTargetingPainter(Ship a, Ship b)
        {
            Cache(a);
            Cache(b);
        }

        public string Name => DiagnosticPainters.GunnerTargeting;

        public void Paint(IDiagnosticCanvas canvas)
        {
            foreach (var gunner in gunners) Draw(canvas, gunner);
        }

        private void Cache(Ship ship)
        {
            if (!ship) return;
            var gunner = ship.GetComponentInChildren<Gunner>();
            if (gunner) gunners.Add(gunner);
        }

        public static void Draw(IDiagnosticCanvas canvas, Gunner gunner)
        {
            if (!gunner.HasTarget) return;
            var target = GamePlane.WorldPointToPlane(gunner.Target);
            DrawTargeting(canvas, GamePlane.WorldPointToPlane(gunner.transform.position), target);
            DrawLineOfSight(canvas, gunner, target);
        }

        private static void DrawTargeting(IDiagnosticCanvas canvas, Vector2 pos, Vector2 target)
        {
            canvas.Line(pos, target, Color.gray);
            canvas.Rect(target, TargetMarkerSize, Color.red);

            var toTarget = target - pos;
            if (toTarget.sqrMagnitude < 1e-8f) return;
            canvas.Line(pos, pos + toTarget.normalized * AimRayLength, Color.red);
        }

        private static void DrawLineOfSight(IDiagnosticCanvas canvas, Gunner gunner, Vector2 target)
        {
            var sight = gunner.weapons?.Sight(WeaponSlot.Primary);
            if (sight == null) return;

            var firePos = sight.FirePoint;
            var firePlane = GamePlane.WorldPointToPlane(firePos);
            canvas.Line(firePlane, target,
                TargetingMath.IsLineClear(firePos, gunner.Target) ? Color.green : Color.red);
            canvas.Ring(firePlane, FirePointRadius, Color.cyan);
        }
    }
}

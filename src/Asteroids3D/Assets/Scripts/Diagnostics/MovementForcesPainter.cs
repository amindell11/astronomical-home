using System.Collections.Generic;
using Ships;
using Ships.Movement;
using UnityEngine;

namespace Game.Diagnostics
{
    public sealed class MovementForcesPainter : IDiagnosticPainter
    {
        private const float MinForce = 0.01f;
        private const float MaxSweepRad = Mathf.PI * 0.25f;
        private const float ArcRadiusFactor = 0.5f;

        private readonly List<MovementController> movers = new();

        public MovementForcesPainter(Ship a, Ship b)
        {
            Cache(a);
            Cache(b);
        }

        public string Name => DiagnosticPainters.MovementForces;

        public void Paint(IDiagnosticCanvas canvas)
        {
            foreach (var mover in movers) Draw(canvas, mover);
        }

        private void Cache(Ship ship)
        {
            if (!ship) return;
            var mover = ship.GetComponentInChildren<MovementController>();
            if (mover) movers.Add(mover);
        }

        public static void Draw(IDiagnosticCanvas canvas, MovementController mover)
        {
            var pos = GamePlane.WorldPointToPlane(mover.transform.position);
            var scale = mover.movementGizmoScale;
            var settings = mover.settings;

            if (mover.dbgThrust.sqrMagnitude > MinForce)
                canvas.Vector(pos, Fraction(mover.dbgThrust, settings?.forwardForce ?? 0f) * scale, Color.yellow);

            if (mover.dbgStrafe.sqrMagnitude > MinForce)
                canvas.Vector(pos, Fraction(mover.dbgStrafe, settings?.maxStrafeForce ?? 0f) * scale, Color.yellow);

            if (mover.dbgBoost.sqrMagnitude > MinForce)
                canvas.Vector(pos, Fraction(mover.dbgBoost, settings?.boostImpulse ?? 0f) * scale * 1.5f, Color.cyan);

            if (Mathf.Abs(mover.dbgYaw) > MinForce)
                DrawYawTorque(canvas, mover, pos, scale, settings?.yawTorque ?? 0f);
        }

        private static void DrawYawTorque(IDiagnosticCanvas canvas, MovementController mover, Vector2 pos, float scale,
            float maxTorque)
        {
            var nose = GamePlane.WorldDirToPlane(mover.transform.up);
            if (nose.sqrMagnitude < 1e-6f) return;
            nose.Normalize();

            var color = mover.dbgYaw > 0f ? Color.green : Color.red;
            var radius = ArcRadiusFactor * scale;
            var sweep = Mathf.Clamp(mover.dbgYaw / (maxTorque > 0f ? maxTorque : 1f), -2f, 2f) * MaxSweepRad;

            canvas.Line(pos, pos + nose * radius, color);
            canvas.Arc(pos, radius, nose, sweep, color);
        }

        private static Vector2 Fraction(Vector2 force, float max) => force / (max > 0f ? max : 1f);
    }
}

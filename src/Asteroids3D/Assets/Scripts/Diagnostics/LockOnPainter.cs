using System.Collections.Generic;
using Combat.Conditions;
using Combat.Targeting;
using Ships;
using UnityEngine;

namespace Game.Diagnostics
{
    /// <summary>Lock-on sensor state in plane-space: sensor-cone ray fan, max-range ring, forward ray, lock line + target ring, lock-progress arc, and a state/lock/cooldown label — all colored by <see cref="LockState"/>. Sensors and their weapon cooldowns are cached at construction.</summary>
    public sealed class LockOnPainter : IDiagnosticPainter
    {
        private const float ConeRayStepDeg = 5f;
        private const float TargetRingRadius = 1f;
        private const float ProgressArcRadius = 2f;

        private readonly List<(LockOnSensor sensor, Cooldown cooldown)> sensors = new();

        public LockOnPainter(Ship a, Ship b)
        {
            Cache(a);
            Cache(b);
        }

        public string Name => DiagnosticPainters.LockOn;

        public void Paint(IDiagnosticCanvas canvas)
        {
            foreach (var (sensor, cooldown) in sensors) Draw(canvas, sensor, cooldown);
        }

        private void Cache(Ship ship)
        {
            if (!ship) return;
            var sensor = ship.GetComponentInChildren<LockOnSensor>();
            if (!sensor) return;
            sensors.Add((sensor, sensor.weapon ? sensor.weapon.GetComponent<Cooldown>() : null));
        }

        public static void Draw(IDiagnosticCanvas canvas, LockOnSensor sensor, Cooldown cooldown)
        {
            if (!sensor.firePoint) return;
            var origin = GamePlane.WorldPointToPlane(sensor.firePoint.position);
            var forward = SafeDir(GamePlane.WorldDirToPlane(sensor.firePoint.up));
            var stateColor = StateColor(sensor.State);

            DrawCone(canvas, sensor, origin, forward, stateColor);
            canvas.Ring(origin, sensor.maxLockDistance, new Color(stateColor.r, stateColor.g, stateColor.b, 0.3f));
            canvas.Line(origin, origin + forward * sensor.maxLockDistance, stateColor);
            DrawTarget(canvas, sensor, origin, stateColor);
            if (sensor.State == LockState.Locking)
                canvas.Arc(origin, ProgressArcRadius, Vector2.right, sensor.LockProgress * 2f * Mathf.PI,
                    Color.Lerp(Color.red, Color.green, sensor.LockProgress));
            DrawLabel(canvas, sensor, cooldown, origin, stateColor);
        }

        private static void DrawCone(IDiagnosticCanvas canvas, LockOnSensor sensor, Vector2 origin, Vector2 forward,
            Color color)
        {
            var halfAngle = sensor.lockOnConeAngle / 2f;
            var faint = new Color(color.r, color.g, color.b, 0.1f);
            var range = sensor.maxLockDistance;

            canvas.Line(origin, origin + Rotate(forward, -halfAngle) * range, faint);
            canvas.Line(origin, origin + Rotate(forward, halfAngle) * range, faint);

            var raysPerSide = Mathf.FloorToInt(halfAngle / ConeRayStepDeg);
            for (var i = 1; i <= raysPerSide; i++)
            {
                var angle = i * ConeRayStepDeg;
                canvas.Line(origin, origin + Rotate(forward, -angle) * range, faint);
                canvas.Line(origin, origin + Rotate(forward, angle) * range, faint);
            }
        }

        private static void DrawTarget(IDiagnosticCanvas canvas, LockOnSensor sensor, Vector2 origin, Color stateColor)
        {
            var target = sensor.CurrentTarget;
            if (target == null || !target.TargetPoint) return;
            var targetPos = GamePlane.WorldPointToPlane(target.TargetPoint.position);
            canvas.Line(origin, targetPos, stateColor);
            canvas.Ring(targetPos, TargetRingRadius, sensor.State == LockState.Locked ? Color.green : Color.red);
        }

        private static void DrawLabel(IDiagnosticCanvas canvas, LockOnSensor sensor, Cooldown cooldown, Vector2 origin,
            Color stateColor)
        {
            var cooldownRemaining = cooldown ? cooldown.CooldownRemaining : 0f;
            canvas.Label(origin + new Vector2(0f, 3f),
                $"Targeting: {sensor.State}\nLock: {sensor.LockProgress:P0}\nCooldown: {cooldownRemaining:F1}s",
                stateColor, 3f);
        }

        private static Color StateColor(LockState state) => state switch
        {
            LockState.Idle => Color.white,
            LockState.Locking => Color.yellow,
            LockState.Locked => Color.green,
            _ => Color.gray,
        };

        private static Vector2 SafeDir(Vector2 v) => v.sqrMagnitude > 1e-8f ? v.normalized : Vector2.up;

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            var rad = degrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(rad);
            var sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }
    }
}

using System.Collections.Generic;
using AI.Scanning;
using Movement.MPC;
using Ships;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Diagnostics
{
    /// <summary>Draws the collision boundaries the MPC actually tests, not cosmetic radii.</summary>
    public sealed class NavigatorObstaclePainter : IDiagnosticPainter
    {
        private static readonly Color ShipRadius = new(0f, 1f, 1f, 0.25f);
        private static readonly Color HullUnbanked = new(1f, 1f, 1f, 0.8f);
        private static readonly Color HullBanked = new(0.3f, 0.8f, 1f, 0.8f);
        private static readonly Color BiteRange = new(1f, 1f, 0f, 0.35f);

        private readonly List<Navigator> navigators = new();

        public NavigatorObstaclePainter(Ship a, Ship b)
        {
            Cache(a);
            Cache(b);
        }

        public string Name => DiagnosticPainters.MpcObstacles;

        public void Paint(IDiagnosticCanvas canvas)
        {
            foreach (var nav in navigators) Draw(canvas, nav);
        }

        private void Cache(Ship ship)
        {
            if (!ship) return;
            var nav = ship.GetComponentInChildren<Navigator>();
            if (nav) navigators.Add(nav);
        }

        public static void Draw(IDiagnosticCanvas canvas, Navigator nav)
        {
            if (nav.mpc == null) return;
            if (nav.dynamics.shipRadius > 0f)
                canvas.Ring(GamePlane.WorldPointToPlane(nav.transform.position), nav.dynamics.shipRadius, ShipRadius);
            if (!nav.scout) return;
            DrawObstacles(canvas, nav, nav.scout.ObstacleScan);
        }

        private static void DrawObstacles(IDiagnosticCanvas canvas, Navigator nav, ObstacleScan scan)
        {
            if (scan.count == 0) return;

            var config = nav.config;
            var profileScale = config.maxBankAngleRad > 0f
                ? Mathf.Cos(Mathf.Abs(nav.lastControl.strafe) * config.maxBankAngleRad)
                : 1f;
            var hullUnbanked = config.shipRadius + config.collisionSafetyMargin;
            var hullCurrent = config.shipRadius * profileScale + config.collisionSafetyMargin;

            var states = nav.predictedStates;
            var speed = states != null && states.Length > 0 ? math.length(states[0].vel) : 0f;
            var halfLatAccel = 0.5f * Mathf.Max(config.maxLatAccel, 1e-4f);

            for (var i = 0; i < scan.count; i++)
            {
                var obs = scan.buffer[i];
                canvas.Ring(obs.position, obs.radius + hullUnbanked, HullUnbanked);

                if (hullCurrent < hullUnbanked - 1e-4f)
                    canvas.Ring(obs.position, obs.radius + hullCurrent, HullBanked);

                // Turn-away bite range: head-on distance inside which lateral thrust can't sidestep a full corridor before impact (½·a_lat·t² == corridor at t = along/speed).
                var corridor = obs.radius + hullCurrent;
                var biteRange = speed > 0.05f ? speed * math.sqrt(corridor / halfLatAccel) : 0f;
                if (biteRange > 0.05f)
                    canvas.Ring(obs.position, corridor + biteRange, BiteRange);
            }
        }
    }
}

using System.Collections.Generic;
using Combat.Projectile;
using Combat.Weapons;
using Game.Services;
using Ships;
using UnityEngine;

namespace Game.Diagnostics
{
    public sealed class MissilesPainter : IDiagnosticPainter
    {
        private const float BodyRingRadius = 0.5f;
        private const float VelocityRayLength = 2f;
        private const float LabelOffset = 1f;
        private const float LauncherLabelOffset = 2f;

        private static readonly Color ExplosionRing = new(1f, 0f, 0f, 0.3f);

        private readonly IProjectileService projectiles;
        private readonly List<Missiles> launchers = new();

        public MissilesPainter(Ship a, Ship b, IProjectileService projectiles)
        {
            this.projectiles = projectiles;
            Cache(a);
            Cache(b);
        }

        public string Name => DiagnosticPainters.Missiles;

        public void Paint(IDiagnosticCanvas canvas)
        {
            projectiles.ForEachLive(live =>
            {
                if (live is Missile missile) Draw(canvas, missile);
            });
            foreach (var launcher in launchers) DrawLauncher(canvas, launcher);
        }

        private void Cache(Ship ship)
        {
            if (!ship) return;
            var launcher = ship.GetComponentInChildren<Missiles>();
            if (launcher) launchers.Add(launcher);
        }

        public static void Draw(IDiagnosticCanvas canvas, Missile missile)
        {
            var pos = GamePlane.WorldPointToPlane(missile.transform.position);
            canvas.Ring(pos, BodyRingRadius, missile.target ? Color.red : Color.yellow);

            var velocity = missile.rb ? GamePlane.WorldDirToPlane(missile.rb.linearVelocity) : Vector2.zero;
            if (velocity.sqrMagnitude > 0.01f)
                canvas.Vector(pos, velocity.normalized * VelocityRayLength, Color.cyan);

            if (missile.target)
                canvas.Line(pos, GamePlane.WorldPointToPlane(missile.target.position), Color.green);

            canvas.Ring(pos, missile.explosionRadius, ExplosionRing);
            canvas.Label(pos + new Vector2(0f, LabelOffset),
                $"Dist: {missile.DistanceTraveled:F1}/{missile.MaxDistance:F1}", Color.white, 3f);
        }

        public static void DrawLauncher(IDiagnosticCanvas canvas, Missiles launcher)
        {
            if (!launcher.firePoint || !launcher.Rounds) return;
            canvas.Label(GamePlane.WorldPointToPlane(launcher.firePoint.position) + new Vector2(0f, LauncherLabelOffset),
                $"Missiles\nAmmo: {launcher.Rounds.AmmoCount}/{launcher.Rounds.MaxAmmo}", Color.white, 3f);
        }
    }
}

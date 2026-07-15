#if UNITY_EDITOR
using AI;
using Combat;
using Combat.Projectile;
using Combat.Weapons;
using Game;
using Game.Capture;
using Ships;
using Ships.Command;
using UnityEngine;

namespace Tests.PlayMode.Common
{

/// <summary>Standard two-ship combat diagnostics for captured clips: per ship a velocity vector, speed label, intercept-aim line (green = primary weapon in envelope) and fire-range ring; plus ship-to-ship LOS (red = blocked) and trails for live projectiles.</summary>
public static class ShipDiagnosticsOverlay
{
    private const float VelocitySecondsShown = 0.6f;
    private const float ProjectileTrail = 2.5f;

    private static readonly Color ShipAColor = new(1f, 0.55f, 0.15f);
    private static readonly Color ShipBColor = new(0.2f, 0.85f, 1f);
    private static readonly Color EnvelopeOpen = new(0.25f, 1f, 0.3f);
    private static readonly Color EnvelopeClosed = new(0.55f, 0.55f, 0.55f);
    private static readonly Color LosClear = new(1f, 1f, 1f, 0.35f);
    private static readonly Color LosBlocked = new(1f, 0.25f, 0.2f);
    private static readonly Color BoltColor = new(1f, 0.95f, 0.2f);

    public static void Draw(CaptureDraw ctx, Ship a, Ship b)
    {
        DrawShip(ctx, a, b, ShipAColor);
        DrawShip(ctx, b, a, ShipBColor);
        DrawLos(ctx, a, b);
        DrawProjectiles(ctx);
    }

    private static void DrawShip(CaptureDraw ctx, Ship ship, Ship enemy, Color color)
    {
        if (!ship || !enemy) return;

        var k = ship.Kinematics;
        ctx.Vector(k.pos, k.vel * VelocitySecondsShown, color);
        ctx.Label(k.pos + new Vector2(0f, 5f), $"{k.vel.magnitude:0.0}", color, 3f);

        var context = ship.Weapons ? ship.Weapons.Context : null;
        var sight = context?.Sight(WeaponSlot.Primary);
        if (sight == null) return;

        var ek = enemy.Kinematics;
        // The exact lead the AI gunner uses. Envelope via InEnvelope only — Evaluate mutates the firing path's LOS cache (observer effect on the sim).
        var aim = Gunner.AimPoint(in k, ek.pos, ek.vel, context.ProjectileSpeed(WeaponSlot.Primary));
        var inEnvelope = sight.InEnvelope(GamePlane.PlanePointToWorld(aim));
        ctx.Line(k.pos, aim, inEnvelope ? EnvelopeOpen : EnvelopeClosed);

        var range = ship.Weapons.Primary ? ship.Weapons.Primary.FireRange : 0f;
        if (range > 0f)
            ctx.Ring(k.pos, range, color * 0.55f);
    }

    private static void DrawLos(CaptureDraw ctx, Ship a, Ship b)
    {
        if (!a || !b) return;
        var from = a.Kinematics.pos;
        var to = b.Kinematics.pos;
        var clear = TargetingMath.IsLineClear(GamePlane.PlanePointToWorld(from), GamePlane.PlanePointToWorld(to));
        ctx.Line(from, to, clear ? LosClear : LosBlocked);
    }

    private static void DrawProjectiles(CaptureDraw ctx)
    {
        var width = ctx.LineWidth;
        ctx.LineWidth = width * 1.6f;
        foreach (var projectile in Object.FindObjectsByType<ProjectileBase>(FindObjectsSortMode.None))
        {
            var rb = projectile.GetComponentInParent<Rigidbody>();
            var dirWorld = rb && rb.linearVelocity.sqrMagnitude > 1e-4f
                ? rb.linearVelocity
                : projectile.transform.up;
            ctx.Trail(GamePlane.WorldPointToPlane(projectile.transform.position),
                GamePlane.WorldDirToPlane(dirWorld), ProjectileTrail, BoltColor);
        }
        ctx.LineWidth = width;
    }
}

} // namespace Tests.PlayMode.Common
#endif

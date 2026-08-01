using System;
using System.Collections.Generic;
using Game.Services;
using Ships;

namespace Game.Diagnostics
{
    /// <summary>What a painter factory is handed: the two subject ships and the projectile service the harness composed. Held instead of an <c>EpisodePair</c> so the contract stays in Game.Core, below the RL harness.</summary>
    public readonly struct PainterContext
    {
        public readonly Ship a;
        public readonly Ship b;
        public readonly IProjectileService projectiles;

        public PainterContext(Ship a, Ship b, IProjectileService projectiles)
        {
            this.a = a;
            this.b = b;
            this.projectiles = projectiles;
        }
    }

    /// <summary>The painter name registry — the selection grammar behind RL_HARNESS_PAINTERS. Only this registry resolves today; the first painter-bearing probe adds probe-sourced painters beside it.</summary>
    public static class DiagnosticPainters
    {
        public const string ShipDiagnostics = "ship-diagnostics";
        public const string Policy = "policy";

        private static readonly Dictionary<string, Func<PainterContext, IDiagnosticPainter>> Factories = new()
        {
            [ShipDiagnostics] = ctx => new ShipDiagnosticsPainter(ctx.a, ctx.b, ctx.projectiles),
            [Policy] = ctx => new PolicyPainter(ctx.a, ctx.b),
        };

        public static string RegisteredNames => string.Join(", ", Factories.Keys);

        public static bool IsRegistered(string name) => name != null && Factories.ContainsKey(name);

        public static IDiagnosticPainter Create(string name, in PainterContext context) =>
            Factories.TryGetValue(name, out var factory)
                ? factory(context)
                : throw new ArgumentException($"No painter named '{name}'; registered painters: {RegisteredNames}.");
    }
}

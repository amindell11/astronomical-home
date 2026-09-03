using UnityEngine;

namespace Game.Services
{
    /// <summary>
    /// The per-session services every ship is wired with: the arena handle its AI reads the world
    /// through, and the projectile pool its weapons draw from. Both the interactive session tier
    /// (<c>SessionHost.ComposeSession</c>) and the RL harness compose them here, so the substrate
    /// under both hosts has one definition instead of two that drift.
    /// Callers supply the <see cref="UnitService"/> — the session's authored sibling or the
    /// harness's <c>AddComponent</c> — so composition never reaches for a lookup.
    /// </summary>
    public static class ShipServices
    {
        public static (ArenaContext arena, ProjectileService projectiles) Compose(
            UnitService units, Transform root, Vector2 offset, bool presentationEnabled)
        {
            var arena = new ArenaContext(offset, units.Registry);
            units.SetArena(arena);

            var projectiles = new ProjectileService(root, presentationEnabled);
            units.SetProjectiles(projectiles);

            return (arena, projectiles);
        }
    }
}

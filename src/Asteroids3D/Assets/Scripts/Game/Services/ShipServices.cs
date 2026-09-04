using UnityEngine;

namespace Game.Services
{
    /// <summary>
    /// The per-session projectile substrate every ship is wired with. Both the interactive session
    /// tier (<c>SessionHost.ComposeSession</c>) and the RL harness compose it here, so the substrate
    /// under both hosts has one definition instead of two that drift. Callers supply the
    /// <see cref="UnitService"/> — the session's authored sibling or the harness's <c>AddComponent</c> —
    /// so composition never reaches for a lookup.
    /// </summary>
    public static class ShipServices
    {
        public static ProjectileService Compose(UnitService units, Transform root, bool presentationEnabled)
        {
            var projectiles = new ProjectileService(root, presentationEnabled);
            units.SetProjectiles(projectiles);
            return projectiles;
        }
    }
}

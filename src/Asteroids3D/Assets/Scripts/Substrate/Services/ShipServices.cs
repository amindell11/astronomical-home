using UnityEngine;
using Substrate.Services.Units;
using Substrate.Services.Projectiles;

namespace Substrate.Services
{
    /// <summary>
    /// The per-session projectile service every ship is wired with, and the presentation policy the
    /// unit service applies to what it spawns. Both the interactive session (<c>Session.Compose</c>)
    /// and the RL harness compose them here, so both hosts share one definition instead of two that
    /// drift. Callers supply the <see cref="UnitService"/> — the session's authored sibling or the
    /// harness's <c>AddComponent</c> — so composition never reaches for a lookup. Ships spawn under
    /// a <c>Units</c> child of <c>root</c>; live projectiles go under <c>transientsRoot</c>, which the
    /// harness leaves as the root itself.
    /// </summary>
    public static class ShipServices
    {
        public static ProjectileService Compose(UnitService units, Transform root, bool presentationEnabled) =>
            Compose(units, root, root, presentationEnabled);

        public static ProjectileService Compose(UnitService units, Transform root, Transform transientsRoot,
            bool presentationEnabled)
        {
            var projectiles = new ProjectileService(transientsRoot, presentationEnabled);
            units.Initialize(projectiles, presentationEnabled, root);
            return projectiles;
        }
    }
}

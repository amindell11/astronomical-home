using System.Collections;
using UnityEngine;

namespace Game.Sectors
{
    /// <summary>Bridges the sector lifecycle to an <see cref="Asteroids.Fields.AsteroidField"/> sibling (its base class blocks deriving <see cref="SectorSpawner"/>); the sector builds under an inactive holder, so the field's Awake has not run during <see cref="Produce"/> — only pre-Awake-safe stashes here, wiring runs in the field's Start.</summary>
    public class AsteroidFieldSpawner : SectorSpawner
    {
        [SerializeField] private Asteroids.Fields.AsteroidField field;

        protected override IEnumerator Produce(SectorBuildContext ctx)
        {
            if (!field) field = GetComponent<Asteroids.Fields.AsteroidField>();
            if (field) field.SetPresentation(ctx.Services.PresentationEnabled);
            if (field is Asteroids.Fields.UpdatingAsteroidField updating)
            {
                // Unity lifetime check, NOT `?.` — the context can hold a destroyed ship and `?.` would pass it through to .transform.
                updating.SetAnchor(ctx.Player ? ctx.Player.transform : null);
                // Declared even in spectator/headless runs so the layout is identical regardless of who is flying.
                if (ctx.Sector) updating.SetPlayerStart(ctx.Sector.PlayerStart);
                // Arena obstacle slot: AI queries live asteroids directly (deterministic) instead of physics-scanning.
                ctx.Services.Arena.ObstacleField = updating;
            }
            yield break;
        }

        protected override IEnumerator OnTeardown(SectorBuildContext ctx)
        {
            if (field is AI.Scanning.IObstacleField of
                && ReferenceEquals(ctx.Services.Arena.ObstacleField, of))
                ctx.Services.Arena.ObstacleField = null;
            if (field) field.DespawnAll();
            yield break;
        }
    }
}

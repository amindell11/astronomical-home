using System;
using System.Collections;
using Substrate.Sessions;
using UnityEngine;

namespace Substrate.Sectors.Elements
{
    /// <summary>Bridges the sector lifecycle to an <see cref="Asteroids.Fields.AsteroidField"/> sibling (its base class blocks deriving <see cref="SectorSpawner"/>); the sector builds under an inactive holder, so the field's Awake has not run during <see cref="Produce"/> — only pre-Awake-safe stashes here, wiring runs in the field's Start. A field that names no preset of its own takes the run row's.</summary>
    public class AsteroidFieldSpawner : SectorSpawner, ISectorRowReader
    {
        [SerializeField] private Asteroids.Fields.AsteroidField field;

        public void CheckRow(SectorEntry row)
        {
            var ownPreset = field && field.settings;
            if (ownPreset == (bool)row.field)
                throw new InvalidOperationException(ownPreset
                    ? $"The asteroid field on '{name}' names its own preset, and the row names another."
                    : $"The asteroid field on '{name}' takes the row's preset, and the row names none.");
        }

        protected override IEnumerator Produce(SectorBuildContext ctx)
        {
            if (!field) field = GetComponent<Asteroids.Fields.AsteroidField>();
            if (field && ctx.Row.field) field.settings = ctx.Row.field;
            if (field) field.SetPresentation(ctx.PresentationEnabled);
            if (field is Asteroids.Fields.UpdatingAsteroidField updating)
            {
                // Unity lifetime check, NOT `?.` — the context can hold a destroyed ship and `?.` would pass it through to .transform.
                updating.SetAnchor(ctx.Hero ? ctx.Hero.transform : null);
                // Declared even in spectator/headless runs so the layout is identical regardless of who is flying.
                if (ctx.Sector) updating.SetStartPoint(ctx.Sector.StartPoint);
            }
            yield break;
        }

        protected override IEnumerator OnTeardown(SectorBuildContext ctx)
        {
            if (field) field.DespawnAll();
            yield break;
        }
    }
}

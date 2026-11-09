using System.Collections;
using System.Collections.Generic;
using Asteroids.Fields.Core;
using Substrate.Sectors.Elements.Stations;
using UnityEngine;

namespace Substrate.Sectors.Elements
{
    /// <summary>Bridges the sector lifecycle to an <see cref="Asteroids.Fields.AsteroidField"/> sibling (its base class blocks deriving <see cref="SectorSpawner"/>); the sector builds under an inactive holder, so the field's Awake has not run during <see cref="Produce"/> — only pre-Awake-safe stashes here, wiring runs in the field's Start. The station clearings are handed over as a source because the station modules pick their points after spawners produce, and the field reads it at Start.</summary>
    public class AsteroidFieldSpawner : SectorSpawner
    {
        [SerializeField] private Asteroids.Fields.AsteroidField field;
        [Tooltip("The sector's repair stations, read for their points and clear radius; unset for none.")]
        [SerializeField] private RepairStation repairs;
        [Tooltip("The sector's mod stations, read for their points and clear radius; unset for none.")]
        [SerializeField] private ModStation mods;

        protected override IEnumerator Produce(SectorBuildContext ctx)
        {
            if (!field) field = GetComponent<Asteroids.Fields.AsteroidField>();
            if (field) field.SetPresentation(ctx.PresentationEnabled);
            if (field is Asteroids.Fields.UpdatingAsteroidField updating)
            {
                // Unity lifetime check, NOT `?.` — the context can hold a destroyed ship and `?.` would pass it through to .transform.
                updating.SetAnchor(ctx.Hero ? ctx.Hero.transform : null);
                // Declared even in spectator/headless runs so the layout is identical regardless of who is flying.
                if (ctx.Sector) updating.SetStartPoint(ctx.Sector.StartPoint);
                updating.SetExclusionVolumes(StationClearings);
            }
            yield break;
        }

        private ExclusionVolume[] StationClearings()
        {
            var volumes = new List<ExclusionVolume>();
            if (repairs)
                foreach (var station in repairs.Stations)
                    volumes.Add(Clearing(station.Position, repairs.ClearRadius));
            if (mods)
                foreach (var station in mods.Stations)
                    volumes.Add(Clearing(station.Position, mods.ClearRadius));
            return volumes.ToArray();
        }

        private static ExclusionVolume Clearing(Vector3 position, float radius) =>
            new() { Center = GamePlane.WorldPointToPlane(position), Radius = radius };

        protected override IEnumerator OnTeardown(SectorBuildContext ctx)
        {
            if (field) field.DespawnAll();
            yield break;
        }
    }
}

using System;
using System.Collections;
using Balance;
using Ships;
using Substrate.Sectors.Elements.Hunt;
using UnityEngine;

namespace Substrate.Sectors.Elements.Stations
{
    /// <summary>
    /// A sector's armories: each fly-in counts one claim in <see cref="Claims"/>, which the host reads to open the
    /// weapon pick. Only a taken weapon spends the station, through <see cref="Take"/>; a skip leaves it live, so
    /// flying back in claims again.
    /// </summary>
    public class Armory : FindStation
    {
        [Tooltip("Station count per sector.")]
        [Stat, SerializeField] private ArmoryFeelProfile feel;

        private Station claimed;

        public override StationKind Kind => StationKind.Armory;

        /// <summary>Fly-ins this sector; the host opens one weapon pick per claim.</summary>
        public int Claims { get; private set; }

        protected override int StationsPerSector => feel.stationsPerSector[SectorIndex];

        protected override float Supply => 1f;

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!feel) throw new InvalidOperationException($"Armory on '{name}' needs its feel profile.");
            if (ctx.SectorIndex < 0 || ctx.SectorIndex >= feel.stationsPerSector.Length)
                throw new InvalidOperationException(
                    $"'{feel.name}' lists armories for {feel.stationsPerSector.Length} sectors, not for sector {ctx.SectorIndex}.");
            Claims = 0;
            claimed = null;
            return base.Setup(ctx);
        }

        /// <summary>The weapon pick for the latest claim was taken: that armory is spent.</summary>
        public void Take()
        {
            if (claimed == null)
                throw new InvalidOperationException($"Armory on '{name}' was taken with no claim open.");
            claimed.Draw(claimed.SupplyLeft);
            claimed = null;
        }

        protected override void Enter(Station station)
        {
            claimed = station;
            Claims++;
        }

        protected override void Visit(Station station, Ship hero, float deltaTime) { }

#if UNITY_EDITOR
        internal void Configure(ArmoryFeelProfile feel, float distance, SectorHunt hunt = null, float spacing = 0f,
            params FindStation[] spacedFrom)
        {
            this.feel = feel;
            ConfigurePlacement(distance, hunt, spacing, spacedFrom);
        }
#endif
    }
}

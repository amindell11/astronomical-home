using System;
using System.Collections;
using Ships;
using Substrate.Sectors.Elements.Hunt;
using Substrate.Sessions;

namespace Substrate.Sectors.Elements.Stations
{
    /// <summary>
    /// A sector's mod stations, as many as the run row names, spaced from its repair stations: the hero's first
    /// fly-in claims one, its whole supply of one claim, and <see cref="Claims"/> counts up, which the host reads
    /// to open one lull pick per claim.
    /// </summary>
    public class ModStation : FindStation, ISectorRowReader
    {
        private int stationCount;

        public override StationKind Kind => StationKind.Mod;

        /// <summary>Stations claimed this sector; the host opens one lull pick per claim.</summary>
        public int Claims { get; private set; }

        protected override int StationsPerSector => stationCount;

        protected override float Supply => 1f;

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            stationCount = ctx.Row.modStations;
            Claims = 0;
            return base.Setup(ctx);
        }

        public void CheckRow(SectorEntry row)
        {
            if (row.modStations < 0)
                throw new InvalidOperationException(
                    $"ModStation on '{name}' places the row's mod stations, and the row asks for {row.modStations}.");
        }

        protected override void Visit(Station station, Ship hero, float deltaTime)
        {
            station.Draw(station.SupplyLeft);
            Claims++;
        }

#if UNITY_EDITOR
        internal void Configure(float distance, SectorHunt hunt = null, float spacing = 0f,
            RepairStation repairs = null)
        {
            ConfigurePlacement(distance, hunt, spacing, repairs ? new FindStation[] { repairs } : Array.Empty<FindStation>());
        }
#endif
    }
}

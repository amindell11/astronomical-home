using System;
using System.Collections;
using Balance;
using Ships;
using Substrate.Sectors.Elements.Hunt;
using UnityEngine;

namespace Substrate.Sectors.Elements.Stations
{
    /// <summary>
    /// A sector's mod stations, spaced from its repair stations: the hero's first fly-in claims one, its whole
    /// supply of one claim, and <see cref="Claims"/> counts up, which the host reads to open one lull pick per claim.
    /// </summary>
    public class ModStation : FindStation
    {
        [Tooltip("Station count.")]
        [Stat, SerializeField] private ModStationFeelProfile feel;

        public override StationKind Kind => StationKind.Mod;

        /// <summary>Stations claimed this sector; the host opens one lull pick per claim.</summary>
        public int Claims { get; private set; }

        protected override int StationsPerSector => feel.stationsPerSector;

        protected override float Supply => 1f;

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!feel) throw new InvalidOperationException($"ModStation on '{name}' needs its feel profile.");
            Claims = 0;
            return base.Setup(ctx);
        }

        protected override void Visit(Station station, Ship hero, float deltaTime)
        {
            station.Draw(station.SupplyLeft);
            Claims++;
        }

#if UNITY_EDITOR
        internal void Configure(ModStationFeelProfile feel, float distance, SectorHunt hunt = null,
            float spacing = 0f, RepairStation repairs = null)
        {
            this.feel = feel;
            ConfigurePlacement(distance, hunt, spacing, repairs ? new FindStation[] { repairs } : Array.Empty<FindStation>());
        }
#endif
    }
}

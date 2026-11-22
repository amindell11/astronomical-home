using System;
using System.Collections;
using Balance;
using Ships;
using Substrate.Sectors.Elements.Hunt;
using Substrate.Sessions;
using UnityEngine;

namespace Substrate.Sectors.Elements.Stations
{
    /// <summary>
    /// A sector's repair stations: while the hero sits in one it restores hull at the feel's rate from that
    /// station's supply, a fraction of a full hull, which drains only by what it heals. The hunt's release
    /// closes one and keeps its supply for the sector. It opens again in a lull, scheduled or earned, and
    /// for a grace window after. The wave director is held to observe, never commanded.
    /// </summary>
    public class RepairStation : FindStation, ISectorRowReader
    {
        [Tooltip("Repair rate and supply.")]
        [Stat, SerializeField] private RepairStationFeelProfile feel;
        [Tooltip("The sector's wave director, read for its lulls; unset, stations stay dark after the hunt's release.")]
        [SerializeField] private WaveDirector director;

        private int stationCount;

        public override StationKind Kind => StationKind.Repair;

        protected override int StationsPerSector => stationCount;

        protected override float Supply => feel.supply;

        protected override bool Open =>
            base.Open || (director && director.InLullWindow(feel.lullGraceSeconds));

        protected override void CloseStation(Station station) => station.Close();

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!feel)
                throw new InvalidOperationException($"RepairStation on '{name}' needs its feel profile.");
            stationCount = ctx.Row.repairStations;
            return base.Setup(ctx);
        }

        public void CheckRow(SectorEntry row)
        {
            if (row.repairStations < 0)
                throw new InvalidOperationException(
                    $"RepairStation on '{name}' places the row's repair stations, and the row asks for {row.repairStations}.");
            CheckDress(row, row.repairStations);
        }

        protected override void Visit(Station station, Ship hero, float deltaTime)
        {
            var hull = hero.Damage.Health;
            var heal = Mathf.Min(feel.repairPerSecond * deltaTime, station.SupplyLeft, 1f - hull.Pct);
            if (heal <= 0f) return;

            hull.RestoreFraction(heal);
            station.Draw(heal);
        }

#if UNITY_EDITOR
        internal void Configure(RepairStationFeelProfile feel, float distance, SectorHunt hunt = null,
            float spacing = 0f, WaveDirector director = null)
        {
            this.feel = feel;
            this.director = director;
            ConfigurePlacement(distance, hunt, spacing);
        }
#endif
    }
}

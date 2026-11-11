using System;
using System.Collections;
using Ships;
using Substrate.Sectors.Elements.Hunt;
using Substrate.Sessions;

namespace Substrate.Sectors.Elements.Stations
{
    /// <summary>
    /// A sector's armories, as many as the run row names: each fly-in counts one claim in <see cref="Claims"/>,
    /// which the host reads to open the weapon pick. Only a taken weapon spends the station, through
    /// <see cref="Take"/>; a skip leaves it live, so flying back in claims again.
    /// </summary>
    public class Armory : FindStation, ISectorRowReader
    {
        private Station claimed;
        private int armoryCount;

        public override StationKind Kind => StationKind.Armory;

        /// <summary>Fly-ins this sector; the host opens one weapon pick per claim.</summary>
        public int Claims { get; private set; }

        protected override int StationsPerSector => armoryCount;

        protected override float Supply => 1f;

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            armoryCount = ctx.Row.armories;
            Claims = 0;
            claimed = null;
            return base.Setup(ctx);
        }

        public void CheckRow(SectorEntry row)
        {
            if (row.armories < 0)
                throw new InvalidOperationException(
                    $"Armory on '{name}' places the row's armories, and the row asks for {row.armories}.");
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
        internal void Configure(float distance, SectorHunt hunt = null, float spacing = 0f,
            params FindStation[] spacedFrom)
        {
            ConfigurePlacement(distance, hunt, spacing, spacedFrom);
        }
#endif
    }
}

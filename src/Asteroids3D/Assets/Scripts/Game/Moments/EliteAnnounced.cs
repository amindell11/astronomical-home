using System;
using System.Collections.Generic;
using System.Globalization;
using Ships.Registry;
using Substrate.Sectors.Elements.Elites;

namespace Game.Moments
{
    public sealed class EliteAnnounced : IRunMoment
    {
        public const string AnnouncedSlot = "elite-announced";

        /// <summary>1-based.</summary>
        public int Sector { get; }
        public string Faction { get; }
        public ShipId Ship { get; }
        public string Slot => AnnouncedSlot;
        public IReadOnlyDictionary<string, string> Facts { get; }

        public EliteAnnounced(EliteMoment moment)
        {
            if (moment.FactionId == null) throw new ArgumentNullException(nameof(moment));
            Sector = moment.SectorIndex + 1;
            Faction = moment.FactionId;
            Ship = moment.Ship;
            Facts = new Dictionary<string, string>
            {
                ["sector"] = Sector.ToString(CultureInfo.InvariantCulture),
                ["faction"] = Faction
            };
        }
    }
}

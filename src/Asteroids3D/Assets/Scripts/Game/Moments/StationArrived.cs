using System;
using System.Collections.Generic;
using System.Globalization;
using Substrate.Sectors.Elements.Stations;

namespace Game.Moments
{
    public sealed class StationArrived : IRunMoment
    {
        public const string RepairSlot = "station-arrived-repair";
        public const string ModSlot = "station-arrived-mod";
        public const string ArmorySlot = "station-arrived-armory";

        public StationKind Kind { get; }

        /// <summary>1-based.</summary>
        public int Sector { get; }
        public string Slot { get; }
        public IReadOnlyDictionary<string, string> Facts { get; }

        public StationArrived(StationKind kind, int sector)
        {
            Slot = kind switch
            {
                StationKind.Repair => RepairSlot,
                StationKind.Mod => ModSlot,
                StationKind.Armory => ArmorySlot,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind,
                    $"No line slot for station kind '{kind}'.")
            };
            Kind = kind;
            Sector = sector;
            Facts = new Dictionary<string, string>
            {
                ["sector"] = sector.ToString(CultureInfo.InvariantCulture)
            };
        }
    }
}

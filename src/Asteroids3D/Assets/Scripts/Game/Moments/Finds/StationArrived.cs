using System;
using System.Collections.Generic;
using System.Globalization;
using Substrate.Sectors.Elements.Stations;
using UnityEngine;

namespace Game.Moments.Finds
{
    public sealed class StationArrived : IRunMoment
    {
        public const string RepairSlot = "station-arrived-repair";
        public const string RepairFullSlot = "station-arrived-repair-full";
        public const string RepairCriticalSlot = "station-arrived-repair-critical";
        public const string ModSlot = "station-arrived-mod";
        public const string ArmorySlot = "station-arrived-armory";

        public const float RepairQuietFrom = 0.7f;
        public const float RepairCriticalBelow = 0.3f;

        /// <summary>1-based.</summary>
        public int Sector { get; }

        /// <summary>The hull fraction on arrival, 0 to 1.</summary>
        public float Hull { get; }

        public string Slot { get; }
        public IReadOnlyDictionary<string, string> Facts { get; }

        /// <returns>Null means the band says nothing.</returns>
        public static StationArrived On(StationKind kind, int sector, float hull)
        {
            switch (kind)
            {
                case StationKind.Repair:
                    if (hull >= 1f)
                        return new StationArrived(RepairFullSlot, sector, hull);
                    if (hull >= RepairQuietFrom)
                        return null;
                    if (hull < RepairCriticalBelow)
                        return new StationArrived(RepairCriticalSlot, sector, hull);
                    return new StationArrived(RepairSlot, sector, hull);
                case StationKind.Mod:
                    return new StationArrived(ModSlot, sector, hull);
                case StationKind.Armory:
                    return new StationArrived(ArmorySlot, sector, hull);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind,
                        $"No line slot for station kind '{kind}'.");
            }
        }

        private StationArrived(string slot, int sector, float hull)
        {
            Slot = slot;
            Sector = sector;
            Hull = hull;
            Facts = new Dictionary<string, string>
            {
                ["sector"] = sector.ToString(CultureInfo.InvariantCulture),
                ["hull"] = Mathf.RoundToInt(hull * 100f).ToString(CultureInfo.InvariantCulture),
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using Ships.Registry;
using Substrate.Sectors.Elements.Hunt;

namespace Game.Moments
{
    public sealed class AlarmRaised : IRunMoment
    {
        public const string AlarmSlot = "alarm-raised";

        /// <summary>1-based.</summary>
        public int Sector { get; }
        public string Faction { get; }
        public HuntAlarmReason Reason { get; }
        public ShipId? Patrol { get; }
        public string Slot => AlarmSlot;
        public IReadOnlyDictionary<string, string> Facts { get; }

        public AlarmRaised(int sector, string faction, HuntAlarmReason reason, ShipId? patrol)
        {
            if (faction == null) throw new ArgumentNullException(nameof(faction));
            var reasonFact = reason switch
            {
                HuntAlarmReason.Patrol => "patrol",
                HuntAlarmReason.SearchClock => "search-clock",
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason,
                    $"No line fact for hunt alarm reason '{reason}'.")
            };
            Sector = sector;
            Faction = faction;
            Reason = reason;
            Patrol = patrol;
            Facts = new Dictionary<string, string>
            {
                ["sector"] = sector.ToString(CultureInfo.InvariantCulture),
                ["faction"] = faction,
                ["reason"] = reasonFact
            };
        }
    }
}

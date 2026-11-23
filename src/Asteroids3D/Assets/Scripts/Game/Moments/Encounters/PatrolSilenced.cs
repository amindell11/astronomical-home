using System;
using System.Collections.Generic;
using System.Globalization;
using Ships.Registry;

namespace Game.Moments.Encounters
{
    public sealed class PatrolSilenced : IRunMoment
    {
        public const string SilencedSlot = "patrol-silenced";

        /// <summary>1-based.</summary>
        public int Sector { get; }
        public string Faction { get; }
        public ShipId Patrol { get; }
        public string Slot => SilencedSlot;
        public IReadOnlyDictionary<string, string> Facts { get; }

        public PatrolSilenced(int sector, string faction, ShipId patrol)
        {
            if (faction == null) throw new ArgumentNullException(nameof(faction));
            Sector = sector;
            Faction = faction;
            Patrol = patrol;
            Facts = new Dictionary<string, string>
            {
                ["sector"] = sector.ToString(CultureInfo.InvariantCulture),
                ["faction"] = faction
            };
        }
    }
}

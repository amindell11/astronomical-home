using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Runs;

namespace Game.Moments.Encounters
{
    public sealed class ShipEarned : IRunMoment
    {
        public const string EarnedSlot = "ship-unlocked";

        /// <summary>1-based.</summary>
        public int Sector { get; }
        public string Faction { get; }
        public string Chassis { get; }
        public bool FirstOfAny { get; }
        public string Slot => EarnedSlot;
        public IReadOnlyDictionary<string, string> Facts { get; }

        public ShipEarned(ShipUnlocked unlocked)
        {
            if (unlocked.FactionId == null) throw new ArgumentNullException(nameof(unlocked));
            Sector = unlocked.SectorIndex + 1;
            Faction = unlocked.FactionId;
            Chassis = unlocked.Chassis;
            FirstOfAny = unlocked.FirstOfAny;
            Facts = new Dictionary<string, string>
            {
                ["sector"] = Sector.ToString(CultureInfo.InvariantCulture),
                ["faction"] = Faction,
                ["ship"] = Chassis
            };
        }
    }
}

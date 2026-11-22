using System;
using System.Collections.Generic;
using System.Globalization;

namespace Game.Moments.Encounters
{
    public sealed class CombatStarted : IRunMoment
    {
        public const string StartedSlot = "combat-start";

        /// <summary>1-based.</summary>
        public int Sector { get; }
        public string Faction { get; }
        public string Slot => StartedSlot;
        public IReadOnlyDictionary<string, string> Facts { get; }

        public CombatStarted(int sector, string faction)
        {
            if (faction == null) throw new ArgumentNullException(nameof(faction));
            Sector = sector;
            Faction = faction;
            Facts = new Dictionary<string, string>
            {
                ["sector"] = sector.ToString(CultureInfo.InvariantCulture),
                ["faction"] = faction
            };
        }
    }
}

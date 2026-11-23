using System;
using System.Collections.Generic;
using System.Globalization;

namespace Game.Moments.Encounters
{
    /// <summary>
    /// A wave's rival died with its minions still flying, so its lines say what the minions do next:
    /// Veil withdraw to regroup, Shoal jump out, Bastion make their last stand.
    /// </summary>
    public sealed class RivalFell : IRunMoment
    {
        public const string FellSlot = "rival-fell";

        /// <summary>1-based.</summary>
        public int Sector { get; }
        public string Faction { get; }
        public string Slot => FellSlot;
        public IReadOnlyDictionary<string, string> Facts { get; }

        public RivalFell(int sector, string faction)
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

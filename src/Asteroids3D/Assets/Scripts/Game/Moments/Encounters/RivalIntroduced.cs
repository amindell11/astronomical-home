using System;
using System.Collections.Generic;
using System.Globalization;
using Ships;

namespace Game.Moments.Encounters
{
    /// <summary>
    /// A sector's first wave introduces the rival leading it. It speaks in place of
    /// that sector's combat start, its lines warn what this rival does, and the host frames the rival up close.
    /// </summary>
    public sealed class RivalIntroduced : IRunMoment
    {
        public const string IntroducedSlot = "rival-introduced";

        /// <summary>1-based.</summary>
        public int Sector { get; }
        public string Faction { get; }
        public Ship Rival { get; }
        public string Slot => IntroducedSlot;
        public IReadOnlyDictionary<string, string> Facts { get; }

        public RivalIntroduced(int sector, string faction, Ship rival)
        {
            if (faction == null) throw new ArgumentNullException(nameof(faction));
            Sector = sector;
            Faction = faction;
            Rival = rival;
            Facts = new Dictionary<string, string>
            {
                ["sector"] = sector.ToString(CultureInfo.InvariantCulture),
                ["faction"] = faction
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using Damage;

namespace Game.Moments
{
    public sealed class PlayerDied : IRunMoment
    {
        public const string DeathSlot = "death";

        /// <summary>1-based.</summary>
        public int Sector { get; }
        public DamageKind KilledBy { get; }
        public string Faction { get; }
        public string Slot => DeathSlot;
        public IReadOnlyDictionary<string, string> Facts { get; }

        public PlayerDied(int sector, DamageKind killedBy, string faction)
        {
            if (faction == null) throw new ArgumentNullException(nameof(faction));
            Sector = sector;
            KilledBy = killedBy;
            Faction = faction;
            Facts = new Dictionary<string, string>
            {
                ["sector"] = sector.ToString(CultureInfo.InvariantCulture),
                ["killed_by"] = killedBy.ToString(),
                ["faction"] = faction,
            };
        }
    }
}

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
        public string Slot => DeathSlot;
        public IReadOnlyDictionary<string, string> Facts { get; }

        public PlayerDied(int sector, DamageKind killedBy)
        {
            Sector = sector;
            KilledBy = killedBy;
            Facts = new Dictionary<string, string>
            {
                ["sector"] = sector.ToString(CultureInfo.InvariantCulture),
                ["killed_by"] = killedBy.ToString(),
            };
        }
    }
}

using System.Collections.Generic;
using System.Globalization;

namespace Game.Moments
{
    public sealed class SectorEntered : IRunMoment
    {
        public const string SectorSlot = "sector-entered";

        /// <summary>1-based.</summary>
        public int Sector { get; }
        public string Slot => SectorSlot;
        public IReadOnlyDictionary<string, string> Facts { get; }

        public SectorEntered(int sector)
        {
            Sector = sector;
            Facts = new Dictionary<string, string> { ["sector"] = sector.ToString(CultureInfo.InvariantCulture) };
        }
    }
}

using System.Collections.Generic;
using System.Globalization;

namespace Game.Moments
{
    public sealed class BeaconClaimed : IRunMoment
    {
        public const string BeaconSlot = "beacon-claimed";

        /// <summary>1-based.</summary>
        public int Sector { get; }
        public string Slot => BeaconSlot;
        public IReadOnlyDictionary<string, string> Facts { get; }

        public BeaconClaimed(int sector)
        {
            Sector = sector;
            Facts = new Dictionary<string, string> { ["sector"] = sector.ToString(CultureInfo.InvariantCulture) };
        }
    }
}

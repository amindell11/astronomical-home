using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Game.Moments
{
    public sealed class CloseCall : IRunMoment
    {
        public const string CloseCallSlot = "close-call";

        /// <summary>1-based.</summary>
        public int Sector { get; }

        /// <summary>The hull fraction left after the hit, 0 to 1.</summary>
        public float Hull { get; }
        public string Slot => CloseCallSlot;
        public IReadOnlyDictionary<string, string> Facts { get; }

        public CloseCall(int sector, float hull)
        {
            Sector = sector;
            Hull = hull;
            Facts = new Dictionary<string, string>
            {
                ["sector"] = sector.ToString(CultureInfo.InvariantCulture),
                ["hull"] = Mathf.RoundToInt(hull * 100f).ToString(CultureInfo.InvariantCulture),
            };
        }
    }
}

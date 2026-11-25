using System;
using UnityEngine;

namespace Substrate.Sectors.Elements.Stations
{
    public static class FindColours
    {
        public static Color Of(StationKind kind)
        {
            // Shades step down in lightness so they stay apart under red-green colour blindness.
            return kind switch
            {
                StationKind.Mod => new Color32(0xFF, 0xD2, 0x7A, 0xFF),
                StationKind.Repair => new Color32(0x5F, 0xE0, 0xC0, 0xFF),
                StationKind.Armory => new Color32(0x8C, 0x9C, 0xFF, 0xFF),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "A station kind with no find colour.")
            };
        }
    }
}

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
                StationKind.Mod => new Color32(0xFF, 0xDA, 0x4A, 0xFF),
                StationKind.Repair => new Color32(0x3C, 0xC0, 0x60, 0xFF),
                StationKind.Armory => new Color32(0xB8, 0x18, 0x38, 0xFF),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "A station kind with no find colour.")
            };
        }
    }
}

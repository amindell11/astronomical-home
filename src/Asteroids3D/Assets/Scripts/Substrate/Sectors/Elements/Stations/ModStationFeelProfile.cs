using Balance;
using UnityEngine;

namespace Substrate.Sectors.Elements.Stations
{
    [CreateAssetMenu(fileName = "ModStationFeel", menuName = "Sectors/Mod Station Feel Profile")]
    public sealed class ModStationFeelProfile : ScriptableObject
    {
        [Tooltip("Mod stations each sector places, each claimed once for one lull pick.")]
        [Stat, Min(0)] public int stationsPerSector = 2;
    }
}

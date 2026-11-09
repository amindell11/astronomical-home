using Balance;
using UnityEngine;

namespace Substrate.Sectors.Elements.Stations
{
    [CreateAssetMenu(fileName = "ArmoryFeel", menuName = "Sectors/Armory Feel Profile")]
    public sealed class ArmoryFeelProfile : ScriptableObject
    {
        [Tooltip("Armories each sector of the run places, the first sector first; each opens the weapon pick.")]
        [Stat(Ordered = true), Min(0)] public int[] stationsPerSector = { 0, 1, 1 };
    }
}

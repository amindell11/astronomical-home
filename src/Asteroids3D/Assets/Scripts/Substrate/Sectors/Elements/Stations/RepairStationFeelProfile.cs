using Balance;
using UnityEngine;

namespace Substrate.Sectors.Elements.Stations
{
    [CreateAssetMenu(fileName = "RepairStationFeel", menuName = "Sectors/Repair Station Feel Profile")]
    public sealed class RepairStationFeelProfile : ScriptableObject
    {
        [Tooltip("Hull restored per second while the player sits in the station, as a fraction of a full hull.")]
        [Stat, Min(0f)] public float repairPerSecond = .2f;

        [Tooltip("Hull the station holds before it goes dark, as a fraction of a full hull.")]
        [Stat, Min(0f)] public float supply = 1f;

        [Tooltip("Seconds into the next sector phase a relit station stays lit.")]
        [Stat, Min(0f)] public float lullGraceSeconds = 10f;
    }
}

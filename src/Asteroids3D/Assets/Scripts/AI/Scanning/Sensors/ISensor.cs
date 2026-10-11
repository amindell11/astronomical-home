using UnityEngine;

namespace AI.Scanning.Sensors
{
    /// <summary>
    /// Low-level physics detection abstraction.
    /// Sensors store their origin transform and auto-detect from that position.
    /// </summary>
    public interface ISensor
    {
        int Detect();
        Collider[] Buffer { get; }
    }
}

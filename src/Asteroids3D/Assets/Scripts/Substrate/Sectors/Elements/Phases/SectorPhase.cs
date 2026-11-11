using System;
using Balance;
using UnityEngine;

namespace Substrate.Sectors.Elements.Phases
{
    [Serializable]
    public struct SectorPhase
    {
        [Tooltip("How long this phase runs before the lull that leads into the next one.")]
        [Stat, Min(1f)] public float seconds;

        [Tooltip("Most enemies alive at once, hunt patrols included. A wave that would pass it waits until it fits.")]
        [Stat, Min(0)] public int cap;

        [Tooltip("The groups this phase's waves draw from, one per wave, picked uniformly. A phase with a cap of 0 lists none.")]
        [Stat] public WaveGroup[] groups;

        [Tooltip("Enemies alive, in whole ships, at or below which the next wave comes after the breather.")]
        [Stat, Min(0)] public int endsAt;

        [Tooltip("Seconds between enemies alive falling to Ends At and the next wave arriving.")]
        [Stat, Min(0f)] public float breather;

        [Tooltip("Most seconds after a wave arrives before the next one comes, however many enemies are still alive.")]
        [Stat, Min(1f)] public float longestWait;
    }

    /// <summary>Exists because Unity cannot serialize a jagged array of ships.</summary>
    [Serializable]
    public struct WaveGroup
    {
        [Tooltip("Ships that arrive together from one bearing, each flying its own pilot.")]
        [Stat] public WaveDirector.RosterEntry[] ships;
    }
}

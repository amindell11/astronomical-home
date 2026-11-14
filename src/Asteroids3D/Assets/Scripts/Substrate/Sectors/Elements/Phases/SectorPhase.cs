using System;
using Balance;
using UnityEngine;
using UnityEngine.Serialization;

namespace Substrate.Sectors.Elements.Phases
{
    [Serializable]
    public struct SectorPhase
    {
        [Tooltip("How long this phase runs before the lull that leads into the next one.")]
        [Stat, Min(1f)] public float seconds;

        [Tooltip("The groups this phase's waves draw from, one per wave, picked uniformly. A phase that lists none sends no waves.")]
        [Stat] public WaveGroup[] groups;

        [Tooltip("Seconds between this phase's waves. The phase opens with one, and another comes on this beat while the phase has time left.")]
        [Stat, Min(1f), FormerlySerializedAs("longestWait")] public float waveEvery;
    }

    /// <summary>Exists because Unity cannot serialize a jagged array of ships.</summary>
    [Serializable]
    public struct WaveGroup
    {
        [Tooltip("Ships that arrive together from one bearing, each flying its own pilot.")]
        [Stat] public WaveDirector.RosterEntry[] ships;
    }
}

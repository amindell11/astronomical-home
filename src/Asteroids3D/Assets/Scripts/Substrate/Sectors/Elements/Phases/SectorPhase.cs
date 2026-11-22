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

        [Tooltip("This phase sends waves, each a rival leading minions. Off, the phase sends none.")]
        [Stat] public bool sendsWaves;

        [Tooltip("Fewest minions flying with each wave's rival.")]
        [Stat, Min(0)] public int fewestMinions;

        [Tooltip("Most minions flying with each wave's rival.")]
        [Stat, Min(0)] public int mostMinions;

        [Tooltip("Seconds between this phase's waves. The phase opens with one, and another comes on this beat while the phase has time left.")]
        [Stat, Min(1f), FormerlySerializedAs("longestWait")] public float waveEvery;
    }
}

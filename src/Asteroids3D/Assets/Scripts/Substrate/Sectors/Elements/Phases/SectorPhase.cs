using System;
using Balance;
using Ships.Loadout;
using UnityEngine;

namespace Substrate.Sectors.Elements.Phases
{
    [Serializable]
    public struct SectorPhase
    {
        [Tooltip("How long this phase runs before the lull that leads into the next one.")]
        [Stat, Min(1f)] public float seconds;

        [Tooltip("Seconds between spawns while under the alive cap.")]
        [Stat, Min(0.1f)] public float interval;

        [Tooltip("Most director ships alive at once. The phase opens by filling to it.")]
        [Stat, Min(0)] public int cap;

        [Tooltip("Shield and weapon pool this phase draws builds from. Unset → the director's pool.")]
        [Stat] public ItemSubset loadouts;
    }
}

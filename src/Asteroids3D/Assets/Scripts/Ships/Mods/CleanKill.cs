using System;
using Loot;
using UnityEngine;

namespace Ships.Mods
{
    /// <summary>The Clean kill passive on the ship that holds it: a sector hunt reads it when the ship kills a patrol that never saw it.</summary>
    [Serializable]
    public sealed class CleanKill
    {
        [Tooltip("The Clean kill passive key this ship reads. Required on a ship that binds run mods.")]
        [SerializeField] private PassiveMod key;

        public bool Held { get; private set; }

        public void Read(HeldBehaviours held) => Held = held.Holds(key);

#if UNITY_EDITOR
        internal void Configure(PassiveMod key) => this.key = key;
#endif
    }
}

using System;
using Balance;
using Combat.Targeting;
using Loot;
using UnityEngine;

namespace Ships.Mods
{
    /// <summary>The Duck and load passive on the ship that holds it: its weapons reload and cool faster while no enemy has sighted it lately.</summary>
    [Serializable]
    public sealed class DuckAndLoad
    {
        [Tooltip("The Duck and load passive key this ship reads. Required on a ship that binds run mods.")]
        [SerializeField] private PassiveMod key;

        [Tooltip("Multiplier on how fast the weapons reload and cool while unseen.")]
        [Stat, SerializeField, Min(1f)] private float recoveryScale = 3f;

        [Tooltip("Seconds since the last enemy sighting after which the ship counts as unseen.")]
        [Stat, SerializeField, Min(0f)] private float unseenAfter = 0.5f;

        public bool Held { get; private set; }

        public void Read(HeldBehaviours held) => Held = held.Holds(key);

        public float RecoveryScale(LockChannel sight, float now) =>
            Held && now - sight.LastSightedAt >= unseenAfter ? recoveryScale : 1f;

#if UNITY_EDITOR
        internal void Configure(PassiveMod key, float recoveryScale, float unseenAfter)
        {
            this.key = key;
            this.recoveryScale = recoveryScale;
            this.unseenAfter = unseenAfter;
        }
#endif
    }
}

using System;
using Balance;
using Loot;
using Substrate;
using UnityEngine;

namespace Ships.Mods
{
    /// <summary>The Backstab passive on the ship that holds it: its hits on a victim's rear arc skip the victim's shield.</summary>
    [Serializable]
    public sealed class Backstab
    {
        [Tooltip("The Backstab passive key this ship reads. Required on a ship that binds run mods.")]
        [SerializeField] private PassiveMod key;

        [Tooltip("Half-angle of the rear arc, in degrees off the victim's tail, where a hit skips the shield.")]
        [Stat, SerializeField, Range(0f, 180f)] private float rearArcHalfAngle = 60f;

        public bool Held { get; private set; }

        public void Read(HeldBehaviours held) => Held = held.Holds(key);

        public bool Pierces(Transform victim, Vector3 hitPoint)
        {
            if (!Held) return false;
            var tail = -GamePlane.WorldDirToPlane(victim.up);
            var toHit = GamePlane.WorldPointToPlane(hitPoint) - GamePlane.WorldPointToPlane(victim.position);
            return toHit.sqrMagnitude > 1e-6f && Vector2.Angle(tail, toHit) <= rearArcHalfAngle;
        }

#if UNITY_EDITOR
        internal void Configure(PassiveMod key, float rearArcHalfAngle)
        {
            this.key = key;
            this.rearArcHalfAngle = rearArcHalfAngle;
        }
#endif
    }
}

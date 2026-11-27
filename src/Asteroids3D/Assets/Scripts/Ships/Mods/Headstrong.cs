using System;
using Balance;
using Loot;
using Substrate;
using UnityEngine;

namespace Ships.Mods
{
    /// <summary>The Headstrong passive on the ship that holds it: its shield counts double against hits on its front arc and not at all behind.</summary>
    [Serializable]
    public sealed class Headstrong
    {
        [Tooltip("The Headstrong passive key this ship reads. Required on a ship that binds run mods.")]
        [SerializeField] private PassiveMod key;

        [Tooltip("Half-angle of the front arc, in degrees off the nose. A hit inside it meets the stronger shield; any other hit skips the shield.")]
        [Stat, SerializeField, Range(0f, 180f)] private float frontArcHalfAngle = 90f;

        [Tooltip("Damage each shield point absorbs from a hit on the front arc.")]
        [Stat, SerializeField, Min(0f)] private float frontShieldScale = 2f;

        public bool Held { get; private set; }

        public void Read(HeldBehaviours held) => Held = held.Holds(key);

        /// <summary>Damage one shield point absorbs from a hit at <paramref name="hitPoint"/>; 0 skips the shield.</summary>
        public float ShieldScale(Transform self, Vector3 hitPoint)
        {
            if (!Held) return 1f;
            var nose = GamePlane.WorldDirToPlane(self.up);
            var toHit = GamePlane.WorldPointToPlane(hitPoint) - GamePlane.WorldPointToPlane(self.position);
            return Vector2.Angle(nose, toHit) <= frontArcHalfAngle ? frontShieldScale : 0f;
        }

#if UNITY_EDITOR
        internal void Configure(PassiveMod key, float frontArcHalfAngle, float frontShieldScale)
        {
            this.key = key;
            this.frontArcHalfAngle = frontArcHalfAngle;
            this.frontShieldScale = frontShieldScale;
        }
#endif
    }
}

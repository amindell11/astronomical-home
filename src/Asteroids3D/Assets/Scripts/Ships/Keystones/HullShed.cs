using System;
using UnityEngine;

namespace Ships.Keystones
{
    /// <summary>
    /// One emitting layer of a <see cref="KeystoneTrail"/>: which part of the hull sheds, and how those particles launch.
    /// </summary>
    [Serializable]
    public sealed class HullShed
    {
        [Tooltip("Particle system this layer emits into. Simulated in world space.")]
        public ParticleSystem system;

        [Tooltip("Particles emitted per ship length travelled while the keystone is active.")]
        [Min(0f)] public float perShipLength;

        [Tooltip("Particles emitted on the frame the keystone becomes active.")]
        [Min(0)] public int launchBurst;

        [Tooltip("Cut along the hull, from -0.5 at the tail to 0.5 at the nose, as a fraction of ship length from the outline centre.")]
        [Range(-0.5f, 0.5f)] public float regionY;

        [Tooltip("Emit from outline points ahead of the cut, toward the nose. Off emits from points behind it.")]
        public bool ahead;

        [Tooltip("Fraction of the ship's velocity a particle starts with.")]
        public float inherit;

        [Tooltip("Outward push along the outline normal, in ship lengths per second.")]
        public float kick;

        [Tooltip("Particle start size, in ship lengths.")]
        [Min(0f)] public float size;

        [Tooltip("Random shrink of the start size. 0 keeps it exact; 1 can shrink it to nothing. Drawn size is size times a random value from (1 - this) to 1.")]
        [Range(0f, 1f)] public float sizeVariation;
    }
}

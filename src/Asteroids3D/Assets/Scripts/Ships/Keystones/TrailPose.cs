using System;
using UnityEngine;

namespace Ships.Keystones
{
    /// <summary>
    /// How a <see cref="KeystoneTrail"/> shapes and lights each particle over its life, as the keystone-trails tuner does:
    /// streaks snap in, stretch with their speed against the ship, whiten while new, then collapse to a sliver.
    /// </summary>
    [Serializable]
    public struct TrailPose
    {
        [Tooltip("How far the body leans from white toward the material tint.")]
        [Range(0f, 1f)] public float tint;

        [Tooltip("Whitening and extra opacity at a streak's birth.")]
        [Range(0f, 1f)] public float flash;

        [Tooltip("How much whiter the newest streaks burn.")]
        [Range(0f, 1f)] public float hotCore;

        [Tooltip("Random spread of each particle's brightness.")]
        [Range(0f, 1f)] public float colorVariation;

        [Tooltip("Random spread of each streak's length.")]
        [Range(0f, 1f)] public float lengthVariation;

        [Tooltip("Streak size at death, as a multiple of its birth size.")]
        [Min(0f)] public float sizeEnd;

        [Tooltip("Extra length per cruise speed of the particle's slide against the ship, capped at three.")]
        [Min(0f)] public float speedStretch;

        [Tooltip("Slide speed, in ship lengths per second, that stretches a streak by one speed stretch.")]
        [Min(0.01f)] public float cruise;

        [Tooltip("Seconds a streak takes to snap in.")]
        [Min(0.01f)] public float snapSeconds;

        [Tooltip("Overshoot of the snap-in.")]
        [Min(0f)] public float overshoot;

        [Tooltip("Extra length at birth, settling to 1 as the streak snaps in.")]
        [Min(1f)] public float stretch;

        [Tooltip("Fraction of the life at the end spent collapsing to a sliver.")]
        [Range(0f, 1f)] public float collapse;
    }
}

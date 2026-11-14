using System;
using UnityEngine;

namespace RL.Opponents
{
    /// <summary>An archetype's shape parameters, authored on a live pilot or set by the matrix lane (and embedded in the episode's JSONL row), then handed to <see cref="ArchetypeBrain.Configure"/>; fields the archetype does not use stay zero.</summary>
    [Serializable]
    public struct OpponentDraw
    {
        [HideInInspector] public string archetype;
        [Tooltip("Caps the velocity law at this fraction of the airframe's max speed: 0.7-1.0, or 0.4-0.6 for the Orbiter — above ~0.6 the orbit slides outside the laser envelope.")]
        public float speedFraction;
        [Tooltip("Evader: seconds between juke flips, 0.6-1.8.")]
        public float jukePeriod;
        [Tooltip("Orbiter: the circling radius, 14-18.")]
        public float orbitRadius;
        [Tooltip("Orbiter: circling direction — positive circles counter-clockwise.")]
        public int orbitDirection;
        [Tooltip("Aggressor/Kiter: the range held on the line of sight, 8-12 for the Aggressor and 14-18 for the Kiter; the laser envelope is 20 u.")]
        public float desiredRange;
    }
}

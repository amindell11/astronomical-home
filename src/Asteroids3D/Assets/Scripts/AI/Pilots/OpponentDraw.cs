using System;
using UnityEngine;

namespace AI.Pilots
{
    /// <summary>An archetype's shape parameters, authored on a live pilot or set by the matrix lane (and embedded in the episode's JSONL row), then handed to <see cref="ArchetypeBrain.Configure"/>; fields the archetype does not use stay zero.</summary>
    [Serializable]
    public struct OpponentDraw
    {
        [HideInInspector] public string archetype;
        [Tooltip("Caps the velocity law at this fraction of the airframe's max speed, 0.7-1.0.")]
        public float speedFraction;
        [Tooltip("Seconds between break-off juke flips, 0.6-1.8.")]
        public float jukePeriod;
        [Tooltip("No law flies this; the matrix row still carries it.")]
        public float orbitRadius;
        [Tooltip("The flank side when no open side reads — positive circles counter-clockwise.")]
        public int orbitDirection;
        [Tooltip("The range held on the line of sight when the weapon declares none, 8-12; the laser envelope is 20 u.")]
        public float desiredRange;
    }
}

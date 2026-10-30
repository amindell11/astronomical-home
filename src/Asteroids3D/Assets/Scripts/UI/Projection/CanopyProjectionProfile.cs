using System;
using UnityEngine;

namespace UI.Projection
{
    [CreateAssetMenu(menuName = "Astronomical/UI/Canopy projection")]
    public sealed class CanopyProjectionProfile : ScriptableObject
    {
        public CanopySettings baseSettings = CanopySettings.Combat;
    }

    [Serializable]
    public struct CanopySettings
    {
        [Range(0f, 2f)] public float glow;
        [Range(0f, 12f)] public float aberration;
        [Range(0f, 1f)] public float curvature;
        [Range(0f, 4f)] public float beamJitter;
        [Range(0f, 1.5f)] public float afterglow;
        [Range(0f, 1f)] public float scanlines;
        [Range(0f, 1f)] public float flicker;
        [Range(0f, 1f)] public float warmth;
        [Range(0f, 1f)] public float wear;
        [Range(0f, 1f)] public float glassDistortion;
        [Range(0f, 18f)] public float damageShudder;
        [Range(0f, 1f)] public float transitionGlitch;

        public static CanopySettings Combat => new()
        {
            glow = .45f, aberration = 1.3f, curvature = .15f, beamJitter = .35f,
            afterglow = .12f, scanlines = .22f, flicker = .3f, warmth = .3f,
            wear = .18f, glassDistortion = .15f, damageShudder = 3f, transitionGlitch = .2f
        };
    }
}

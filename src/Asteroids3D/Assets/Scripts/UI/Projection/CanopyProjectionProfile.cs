using System;
using UnityEngine;

namespace UI.Projection
{
    [CreateAssetMenu(menuName = "Astronomical/UI/Canopy projection")]
    public sealed class CanopyProjectionProfile : ScriptableObject
    {
        public bool personalityEnabled = true;
        public CanopySettings baseSettings = CanopySettings.Combat;
        public CanopyStateOverride lowHull = new();
        public CanopyStateOverride bigMoment = new()
        {
            fields = CanopyFields.All,
            settings = new CanopySettings
            {
                glow = 1.1f, aberration = 4f, beamJitter = 1.3f, afterglow = .5f,
                scanlines = .5f, flicker = .6f, warmth = .4f, wear = .25f,
                glassDistortion = .35f, damageShudder = 5f, transitionGlitch = .7f
            }
        };
        [Min(0f)] public float bigMomentSeconds = 1f;

        public CanopySettings Resolve(CanopyState state) => state == CanopyState.Base
            ? baseSettings : OverrideFor(state).Apply(baseSettings);

        public CanopyStateOverride OverrideFor(CanopyState state) => state switch
        {
            CanopyState.LowHull => lowHull,
            CanopyState.BigMoment => bigMoment,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Base has no override block.")
        };
    }

    [Serializable]
    public struct CanopySettings
    {
        [Range(0f, 2f)] public float glow;
        [Range(0f, 12f)] public float aberration;
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
            glow = .45f, aberration = 1.3f, beamJitter = .35f,
            afterglow = .12f, scanlines = .22f, flicker = .3f, warmth = .3f,
            wear = .18f, glassDistortion = .15f, damageShudder = 3f, transitionGlitch = .2f
        };

        public static CanopySettings Lerp(CanopySettings from, CanopySettings to, float amount) => new()
        {
            glow = Mathf.Lerp(from.glow, to.glow, amount),
            aberration = Mathf.Lerp(from.aberration, to.aberration, amount),
            beamJitter = Mathf.Lerp(from.beamJitter, to.beamJitter, amount),
            afterglow = Mathf.Lerp(from.afterglow, to.afterglow, amount),
            scanlines = Mathf.Lerp(from.scanlines, to.scanlines, amount),
            flicker = Mathf.Lerp(from.flicker, to.flicker, amount),
            warmth = Mathf.Lerp(from.warmth, to.warmth, amount),
            wear = Mathf.Lerp(from.wear, to.wear, amount),
            glassDistortion = Mathf.Lerp(from.glassDistortion, to.glassDistortion, amount),
            damageShudder = Mathf.Lerp(from.damageShudder, to.damageShudder, amount),
            transitionGlitch = Mathf.Lerp(from.transitionGlitch, to.transitionGlitch, amount)
        };
    }
}

using System;
using UnityEngine;

namespace UI.Projection
{
    [Serializable]
    public sealed class CanopyStateOverride
    {
        public CanopyFields fields;
        public CanopySettings settings;
        [Min(0f)] public float blendInSeconds = .15f;
        [Min(0f)] public float blendOutSeconds = .35f;

        public CanopySettings Apply(CanopySettings value)
        {
            if ((fields & CanopyFields.Glow) != 0) value.glow = settings.glow;
            if ((fields & CanopyFields.Aberration) != 0) value.aberration = settings.aberration;
            if ((fields & CanopyFields.BeamJitter) != 0) value.beamJitter = settings.beamJitter;
            if ((fields & CanopyFields.Afterglow) != 0) value.afterglow = settings.afterglow;
            if ((fields & CanopyFields.Scanlines) != 0) value.scanlines = settings.scanlines;
            if ((fields & CanopyFields.Flicker) != 0) value.flicker = settings.flicker;
            if ((fields & CanopyFields.Warmth) != 0) value.warmth = settings.warmth;
            if ((fields & CanopyFields.Wear) != 0) value.wear = settings.wear;
            if ((fields & CanopyFields.GlassDistortion) != 0) value.glassDistortion = settings.glassDistortion;
            if ((fields & CanopyFields.DamageShudder) != 0) value.damageShudder = settings.damageShudder;
            if ((fields & CanopyFields.TransitionGlitch) != 0) value.transitionGlitch = settings.transitionGlitch;
            return value;
        }
    }

    [Flags]
    public enum CanopyFields
    {
        None = 0, Glow = 1, Aberration = 2, BeamJitter = 4, Afterglow = 8, Scanlines = 16,
        Flicker = 32, Warmth = 64, Wear = 128, GlassDistortion = 256, DamageShudder = 512,
        TransitionGlitch = 1024, All = 2047
    }
}

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
            if ((fields & CanopyFields.HaloReach) != 0) value.haloReach = settings.haloReach;
            if ((fields & CanopyFields.HaloStrength) != 0) value.haloStrength = settings.haloStrength;
            if ((fields & CanopyFields.TextGlow) != 0) value.textGlow = settings.textGlow;
            if ((fields & CanopyFields.Halation) != 0) value.halation = settings.halation;
            if ((fields & CanopyFields.CoolTint) != 0) value.coolTint = settings.coolTint;
            if ((fields & CanopyFields.SignalBleed) != 0) value.signalBleed = settings.signalBleed;
            if ((fields & CanopyFields.BleedLength) != 0) value.bleedLength = settings.bleedLength;
            return value;
        }
    }

    [Flags]
    public enum CanopyFields
    {
        None = 0, Glow = 1, Aberration = 2, BeamJitter = 4, Afterglow = 8, Scanlines = 16,
        Flicker = 32, Warmth = 64, Wear = 128, GlassDistortion = 256, DamageShudder = 512,
        TransitionGlitch = 1024, HaloReach = 2048, HaloStrength = 4096, TextGlow = 8192,
        Halation = 16384, CoolTint = 32768, SignalBleed = 65536, BleedLength = 131072, All = 262143
    }
}

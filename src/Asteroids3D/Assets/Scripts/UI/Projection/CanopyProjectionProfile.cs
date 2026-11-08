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
                glassDistortion = .35f, damageShudder = 5f, transitionGlitch = .7f,
                haloReach = 14f, haloStrength = 1f, textGlow = .2f, halation = .8f,
                coolTint = new Color(.35f, .62f, 1f, 1f), signalBleed = 1.2f, bleedLength = 14f
            }
        };
        [Min(0f)] public float bigMomentSeconds = 1f;
        [Header("Enemy status bars")]
        [Tooltip("How near the cursor must come to an enemy, as a fraction of the screen's height, to show its bars and numbers.")]
        [Min(0f)] public float targetPickRadius = .1f;
        [Tooltip("Seconds an enemy the player hits keeps its bars, without numbers.")]
        [Min(0f)] public float hitBarsSeconds = 3f;
        [Tooltip("Seconds an enemy's bars take to fade in when it becomes the target or is hit.")]
        [Min(0f)] public float barsFadeInSeconds = .15f;
        [Tooltip("Seconds an enemy's bars take to fade out once nothing keeps them shown.")]
        [Min(0f)] public float barsFadeOutSeconds = .4f;

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
        [Tooltip("Reach of the wide halo, in pixels on a 1600-wide frame.")]
        [Range(0f, 32f)] public float haloReach;
        [Tooltip("Brightness of the wide halo.")]
        [Range(0f, 2f)] public float haloStrength;
        [Tooltip("Share of text light that feeds both halos.")]
        [Range(0f, 1f)] public float textGlow;
        [Tooltip("How far both halos shift toward the cool tint.")]
        [Range(0f, 1f)] public float halation;
        [Tooltip("The blue that halation and signal bleed take on.")]
        [ColorUsage(false)] public Color coolTint;
        [Tooltip("Brightness of the trail to the right of bright strokes.")]
        [Range(0f, 2f)] public float signalBleed;
        [Tooltip("Length of the signal bleed trail, in pixels on a 1600-wide frame.")]
        [Range(0f, 24f)] public float bleedLength;

        public static CanopySettings Combat => new()
        {
            glow = .45f, aberration = 1.3f, beamJitter = .35f,
            afterglow = .12f, scanlines = .22f, flicker = .3f, warmth = .3f,
            wear = .18f, glassDistortion = .15f, damageShudder = 3f, transitionGlitch = .2f,
            haloReach = 10f, haloStrength = .6f, textGlow = 0f, halation = .7f,
            coolTint = new Color(.35f, .62f, 1f, 1f), signalBleed = .8f, bleedLength = 10f
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
            transitionGlitch = Mathf.Lerp(from.transitionGlitch, to.transitionGlitch, amount),
            haloReach = Mathf.Lerp(from.haloReach, to.haloReach, amount),
            haloStrength = Mathf.Lerp(from.haloStrength, to.haloStrength, amount),
            textGlow = Mathf.Lerp(from.textGlow, to.textGlow, amount),
            halation = Mathf.Lerp(from.halation, to.halation, amount),
            coolTint = Color.Lerp(from.coolTint, to.coolTint, amount),
            signalBleed = Mathf.Lerp(from.signalBleed, to.signalBleed, amount),
            bleedLength = Mathf.Lerp(from.bleedLength, to.bleedLength, amount)
        };
    }
}

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Substrate.Services.Locales
{
    /// <summary>
    /// The <see cref="RenderSettings"/> values a locale owns. A <see cref="LocaleSky"/> serializes one and writes it
    /// while its locale is shown; the locale service captures the active scene's values in the same shape, so restoring
    /// them overwrites every value a locale wrote.
    /// </summary>
    [Serializable]
    public struct LocaleLighting
    {
        public AmbientMode ambientMode;
        [ColorUsage(false, true)] public Color ambientSkyColor;
        [ColorUsage(false, true)] public Color ambientEquatorColor;
        [ColorUsage(false, true)] public Color ambientGroundColor;
        public float ambientIntensity;
        public bool fog;
        public FogMode fogMode;
        public Color fogColor;
        public float fogDensity;
        public float fogStartDistance;
        public float fogEndDistance;
        public DefaultReflectionMode reflectionMode;
        public Texture reflection;
        public float reflectionIntensity;
        [Tooltip("This locale's own directional light; unassigned lets the brightest directional light lead.")]
        public Light sun;

        public static LocaleLighting Capture() => new()
        {
            ambientMode = RenderSettings.ambientMode,
            ambientSkyColor = RenderSettings.ambientSkyColor,
            ambientEquatorColor = RenderSettings.ambientEquatorColor,
            ambientGroundColor = RenderSettings.ambientGroundColor,
            ambientIntensity = RenderSettings.ambientIntensity,
            fog = RenderSettings.fog,
            fogMode = RenderSettings.fogMode,
            fogColor = RenderSettings.fogColor,
            fogDensity = RenderSettings.fogDensity,
            fogStartDistance = RenderSettings.fogStartDistance,
            fogEndDistance = RenderSettings.fogEndDistance,
            reflectionMode = RenderSettings.defaultReflectionMode,
            reflection = RenderSettings.customReflectionTexture,
            reflectionIntensity = RenderSettings.reflectionIntensity,
            sun = RenderSettings.sun
        };

        public void Apply()
        {
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = ambientSkyColor;
            RenderSettings.ambientEquatorColor = ambientEquatorColor;
            RenderSettings.ambientGroundColor = ambientGroundColor;
            RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.fog = fog;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogStartDistance = fogStartDistance;
            RenderSettings.fogEndDistance = fogEndDistance;
            RenderSettings.defaultReflectionMode = reflectionMode;
            RenderSettings.customReflectionTexture = reflection;
            RenderSettings.reflectionIntensity = reflectionIntensity;
            RenderSettings.sun = sun;
        }
    }
}

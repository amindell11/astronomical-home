using Substrate.Services.Locales;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tests.PlayMode.Common
{
    /// <summary>
    /// The test scene's lighting before a fixture shows a locale. Showing one writes the scene's
    /// <see cref="RenderSettings"/>, so a fixture whose host never tears down restores them for the next fixture.
    /// </summary>
    public readonly struct LightingSnapshot
    {
        private readonly LocaleLighting lighting;
        private readonly SphericalHarmonicsL2 probe;

        private LightingSnapshot(LocaleLighting lighting, SphericalHarmonicsL2 probe)
        {
            this.lighting = lighting;
            this.probe = probe;
        }

        public Color Ambient => lighting.ambientSkyColor;

        public static LightingSnapshot Capture() => new(LocaleLighting.Capture(), RenderSettings.ambientProbe);

        public void Restore()
        {
            lighting.Apply();
            RenderSettings.ambientProbe = probe;
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Substrate.Services.Locales
{
    /// <summary>
    /// Renders a camera under each locale's sky and lighting, restoring the active world afterward.
    /// The locale service owns loading; callers blend the resulting images without a lighting switch.
    /// </summary>
    public sealed class LocaleBackdropPair : MonoBehaviour
    {
        private LocaleSky[] skies;
        private LocaleSky outgoing;
        private LocaleSky incoming;
        private Action release;
        private Lighting outgoingLighting;
        private Lighting incomingLighting;

        private void Awake() => skies = FindObjectsByType<LocaleSky>(FindObjectsSortMode.None);

        internal void Initialize(Scene from, Scene to, Action releaseDestination)
        {
            outgoing = RequireSky(from);
            incoming = RequireSky(to);
            release = releaseDestination;
            var active = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(from);
            outgoingLighting = new Lighting();
            SceneManager.SetActiveScene(to);
            incomingLighting = new Lighting();
            SceneManager.SetActiveScene(active);
        }

        private LocaleSky RequireSky(Scene scene)
        {
            foreach (var sky in skies)
                if (sky.gameObject.scene == scene) return sky;
            throw new InvalidOperationException($"Locale '{scene.name}' has no LocaleSky.");
        }

        public void Render(Camera camera, RenderTexture from, RenderTexture to)
        {
            var previous = RenderTexture.active;
            var target = camera.targetTexture;
            try
            {
                incoming.SetVisible(false);
                outgoing.SetVisible(true);
                outgoingLighting.Apply();
                camera.targetTexture = from;
                camera.Render();
                outgoing.SetVisible(false);
                incoming.SetVisible(true);
                incomingLighting.Apply();
                camera.targetTexture = to;
                camera.Render();
            }
            finally
            {
                var active = SceneManager.GetActiveScene();
                outgoing.SetVisible(outgoing.gameObject.scene == active);
                incoming.SetVisible(incoming.gameObject.scene == active);
                (outgoing.gameObject.scene == active ? outgoingLighting : incomingLighting).Apply();
                camera.targetTexture = target;
                RenderTexture.active = previous;
            }
        }

        private void OnDestroy() => release?.Invoke();

        private sealed class Lighting
        {
            private readonly UnityEngine.Rendering.AmbientMode mode = RenderSettings.ambientMode;
            private readonly Color sky = RenderSettings.ambientSkyColor;
            private readonly Color equator = RenderSettings.ambientEquatorColor;
            private readonly Color ground = RenderSettings.ambientGroundColor;
            private readonly float intensity = RenderSettings.ambientIntensity;
            private readonly UnityEngine.Rendering.SphericalHarmonicsL2 probe = RenderSettings.ambientProbe;
            private readonly Light sun = RenderSettings.sun;

            public void Apply()
            {
                RenderSettings.ambientMode = mode;
                RenderSettings.ambientSkyColor = sky;
                RenderSettings.ambientEquatorColor = equator;
                RenderSettings.ambientGroundColor = ground;
                RenderSettings.ambientIntensity = intensity;
                RenderSettings.ambientProbe = probe;
                RenderSettings.sun = sun;
            }
        }
    }
}

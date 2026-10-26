using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Substrate.Services.Locales
{
    /// <summary>
    /// Renders two loaded locale skies independently without changing the active lighting scene.
    /// The locale service owns their loading; this view restores visibility after every render.
    /// </summary>
    public sealed class LocaleBackdropPair : MonoBehaviour
    {
        private LocaleSky[] skies;
        private LocaleSky outgoing;
        private LocaleSky incoming;
        private Action release;

        private void Awake() => skies = FindObjectsByType<LocaleSky>(FindObjectsSortMode.None);

        internal void Initialize(Scene from, Scene to, Action releaseDestination)
        {
            outgoing = RequireSky(from);
            incoming = RequireSky(to);
            release = releaseDestination;
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
                outgoing.SetVisible(true);
                incoming.SetVisible(false);
                camera.targetTexture = from;
                camera.Render();
                outgoing.SetVisible(false);
                incoming.SetVisible(true);
                camera.targetTexture = to;
                camera.Render();
            }
            finally
            {
                var active = SceneManager.GetActiveScene();
                outgoing.SetVisible(outgoing.gameObject.scene == active);
                incoming.SetVisible(incoming.gameObject.scene == active);
                camera.targetTexture = target;
                RenderTexture.active = previous;
            }
        }

        private void OnDestroy() => release?.Invoke();
    }
}

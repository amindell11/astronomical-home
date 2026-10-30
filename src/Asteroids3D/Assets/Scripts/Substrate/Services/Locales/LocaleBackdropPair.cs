using System;
using UnityEngine;

namespace Substrate.Services.Locales
{
    /// <summary>
    /// Renders a camera under each of two locales' sky and lighting, then restores the shown locale.
    /// The locale service owns both instances; callers blend the resulting images without a lighting switch.
    /// </summary>
    public sealed class LocaleBackdropPair : MonoBehaviour
    {
        private LocaleSky outgoing;
        private LocaleSky incoming;
        private Action release;

        internal void Initialize(LocaleSky from, LocaleSky to, Action releaseDestination)
        {
            outgoing = from;
            incoming = to;
            release = releaseDestination;
        }

        public void Render(Camera camera, RenderTexture from, RenderTexture to)
        {
            var previous = RenderTexture.active;
            var target = camera.targetTexture;
            var shown = incoming.Shown ? incoming : outgoing;
            try
            {
                incoming.SetVisible(false);
                outgoing.SetVisible(true);
                outgoing.ApplyLighting();
                camera.targetTexture = from;
                camera.Render();
                outgoing.SetVisible(false);
                incoming.SetVisible(true);
                incoming.ApplyLighting();
                camera.targetTexture = to;
                camera.Render();
            }
            finally
            {
                (shown == incoming ? outgoing : incoming).SetVisible(false);
                shown.SetVisible(true);
                shown.ApplyLighting();
                camera.targetTexture = target;
                RenderTexture.active = previous;
            }
        }

        private void OnDestroy() => release?.Invoke();
    }
}

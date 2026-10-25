using System;
using System.Collections;
using Cameras;
using Substrate;
using UI;
using UnityEngine;

namespace Game.Travel
{
    [Serializable]
    public sealed class SectorJump : IDisposable
    {
        [SerializeField, Min(0f)] internal float duration = 1.5f;
        [SerializeField] private Vector2 travel = new(20000f, -20000f);

        private ObserverCam observer;
        private Overlay overlay;
        private Vector3 position;
        private int cullingMask;
        private bool observerEnabled;
        private bool overlayVisible;
        private float timeScale;
        private bool playing;

        public IEnumerator Play(ObserverCam camera, Overlay hud)
        {
            observer = camera;
            overlay = hud;
            position = observer.transform.position;
            cullingMask = observer.Cam.cullingMask;
            observerEnabled = observer.enabled;
            overlayVisible = overlay && overlay.IsVisible;
            timeScale = Time.timeScale;
            playing = true;

            observer.enabled = false;
            observer.Cam.cullingMask = 1 << LayerIds.Sky;
            if (overlay) overlay.SetVisible(false);
            Time.timeScale = 0f;

            try
            {
                for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                {
                    var progress = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                    observer.transform.position = position + GamePlane.PlaneDirToWorld(travel * progress);
                    yield return null;
                }
            }
            finally
            {
                Dispose();
            }
        }

        public void Dispose()
        {
            if (!playing) return;
            playing = false;
            Time.timeScale = timeScale;
            if (observer)
            {
                observer.transform.position = position;
                observer.Cam.cullingMask = cullingMask;
                observer.enabled = observerEnabled;
            }
            if (overlay) overlay.SetVisible(overlayVisible);
            observer = null;
            overlay = null;
        }
    }
}

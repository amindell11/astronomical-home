using System;
using System.Collections;
using Cameras;
using Ships;
using Substrate;
using UnityEngine;

namespace Game.CloseUps
{
    /// <summary>
    /// Shows the player something away from their ship: time slows while the flight camera pans and zooms onto a
    /// point, holds, then eases back to the player as time speeds up. Every step runs on unscaled time. A target
    /// that goes away cuts the hold short; a cancel ends at once. Either way the camera goes back to following and
    /// the slow closes. The rival close-up follows a ship; the guided first sector also frames a patrol and the
    /// sector's stations.
    /// </summary>
    internal static class CloseUp
    {
        public static IEnumerator Run(CloseUpFeel feel, ObserverCam observer, Transform player, Ship ship,
            ClockSlow slow, Func<bool> cancelled) =>
            Run(feel, observer, player, () => Present(ship) ? GamePlane.WorldPointToPlane(ship.transform.position) : (Vector2?)null,
                feel.closeSize, slow, cancelled);

        // target reads null once the subject has gone.
        public static IEnumerator Run(CloseUpFeel feel, ObserverCam observer, Transform player, Func<Vector2?> target,
            float size, ClockSlow slow, Func<bool> cancelled)
        {
            if (feel == null) throw new ArgumentNullException(nameof(feel));
            if (!observer) throw new ArgumentNullException(nameof(observer));
            if (!player) throw new ArgumentNullException(nameof(player));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (slow == null) throw new ArgumentNullException(nameof(slow));
            if (cancelled == null) throw new ArgumentNullException(nameof(cancelled));

            var startCenter = GamePlane.WorldPointToPlane(observer.transform.position);
            var startSize = observer.Cam.orthographicSize;
            var at = startCenter;
            try
            {
                for (var t = 0f; t < feel.easeInSeconds; t += Time.unscaledDeltaTime)
                {
                    if (cancelled()) yield break;
                    if (target() is not { } now) break;
                    at = now;
                    var k = Ease(t / feel.easeInSeconds);
                    Frame(observer, Vector2.Lerp(startCenter, at, k), Mathf.Lerp(startSize, size, k));
                    slow.Scale = Mathf.Lerp(1f, feel.slowScale, k);
                    yield return null;
                }

                for (var t = 0f; t < feel.holdSeconds && target() is { } now; t += Time.unscaledDeltaTime)
                {
                    if (cancelled()) yield break;
                    at = now;
                    Frame(observer, at, size);
                    slow.Scale = feel.slowScale;
                    yield return null;
                }

                var fromSize = observer.Cam.orthographicSize;
                var fromScale = slow.Scale;
                for (var t = 0f; t < feel.easeOutSeconds; t += Time.unscaledDeltaTime)
                {
                    if (cancelled() || !player) yield break;
                    var k = Ease(t / feel.easeOutSeconds);
                    var playerAt = GamePlane.WorldPointToPlane(player.position);
                    Frame(observer, Vector2.Lerp(at, playerAt, k), Mathf.Lerp(fromSize, startSize, k));
                    slow.Scale = Mathf.Lerp(fromScale, 1f, k);
                    yield return null;
                }
            }
            finally
            {
                slow.Dispose();
                observer.SetManualCenter(null);
                observer.SetManualZoom(null);
                observer.ResumeFollow();
            }
        }

        private static bool Present(Ship ship) => ship && ship.gameObject.activeInHierarchy;

        private static void Frame(ObserverCam observer, Vector2 center, float size)
        {
            observer.SetManualCenter(center);
            observer.SetManualZoom(size);
        }

        private static float Ease(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
    }
}

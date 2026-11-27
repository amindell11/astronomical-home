using System;
using System.Collections;
using Cameras;
using Ships;
using Substrate;
using UnityEngine;

namespace Game.CloseUps
{
    /// <summary>
    /// Shows the player the rival leading a sector's first wave: time slows while the flight camera pans and zooms onto
    /// it, holds, then eases back to the player as time speeds up. Every step runs on unscaled time. A rival
    /// that dies or leaves cuts the hold short; a cancel ends at once. Either way the camera goes back to
    /// following and the slow closes.
    /// </summary>
    internal static class RivalCloseUp
    {
        public static IEnumerator Run(CloseUpFeel feel, ObserverCam observer, Transform player, Ship rival,
            ClockSlow slow, Func<bool> cancelled)
        {
            if (feel == null) throw new ArgumentNullException(nameof(feel));
            if (!observer) throw new ArgumentNullException(nameof(observer));
            if (!player) throw new ArgumentNullException(nameof(player));
            if (slow == null) throw new ArgumentNullException(nameof(slow));
            if (cancelled == null) throw new ArgumentNullException(nameof(cancelled));

            var startCenter = GamePlane.WorldPointToPlane(observer.transform.position);
            var startSize = observer.Cam.orthographicSize;
            var rivalAt = startCenter;
            try
            {
                for (var t = 0f; t < feel.easeInSeconds; t += Time.unscaledDeltaTime)
                {
                    if (cancelled()) yield break;
                    if (!Present(rival)) break;
                    rivalAt = GamePlane.WorldPointToPlane(rival.transform.position);
                    var k = Ease(t / feel.easeInSeconds);
                    Frame(observer, Vector2.Lerp(startCenter, rivalAt, k), Mathf.Lerp(startSize, feel.closeSize, k));
                    slow.Scale = Mathf.Lerp(1f, feel.slowScale, k);
                    yield return null;
                }

                for (var t = 0f; t < feel.holdSeconds && Present(rival); t += Time.unscaledDeltaTime)
                {
                    if (cancelled()) yield break;
                    rivalAt = GamePlane.WorldPointToPlane(rival.transform.position);
                    Frame(observer, rivalAt, feel.closeSize);
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
                    Frame(observer, Vector2.Lerp(rivalAt, playerAt, k), Mathf.Lerp(fromSize, startSize, k));
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

        private static bool Present(Ship rival) => rival && rival.gameObject.activeInHierarchy;

        private static void Frame(ObserverCam observer, Vector2 center, float size)
        {
            observer.SetManualCenter(center);
            observer.SetManualZoom(size);
        }

        private static float Ease(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
    }
}

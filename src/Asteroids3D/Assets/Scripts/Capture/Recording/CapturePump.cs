using System;
using System.Collections;
using UnityEngine;

namespace Capture.Recording
{
    internal sealed class CapturePump : MonoBehaviour
    {
        public void Begin(Func<bool> acquire) => StartCoroutine(Frames(acquire));

        private static IEnumerator Frames(Func<bool> acquire)
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (true)
            {
                yield return endOfFrame;
                if (!acquire()) yield break;
            }
        }
    }
}

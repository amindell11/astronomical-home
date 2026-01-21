using System;
using UnityEngine;

namespace Utils
{
    /// <summary>
    /// Caches a value for one frame to avoid redundant computations.
    /// Thread-safe within Unity's main thread context.
    /// </summary>
    public class FrameCache<T>
    {
        private T cachedValue;
        private int cachedFrame = -1;

        public T Get(Func<T> compute)
        {
            var currentFrame = Time.frameCount;
            if (currentFrame == cachedFrame) return cachedValue;
            
            cachedValue = compute();
            cachedFrame = currentFrame;
            return cachedValue;
        }

        public void Invalidate() => cachedFrame = -1;
    }
}

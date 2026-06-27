using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Frame-coherent line-of-sight check for a single fire-point. Holds the only mutable state
    /// in targeting: the last raycast result and when/where it was taken, so a weapon polling LOS
    /// every tick doesn't raycast every tick. One per fire-point (not per ship), since the cache
    /// keys on the firing position.
    /// </summary>
    public sealed class LosCache
    {
        /// <summary>How many frames a cached raycast result is reused before re-casting.</summary>
        private const int CacheFrames = 5;

        /// <summary>Aim error (degrees) beyond which we skip the raycast — you can't fire there anyway.</summary>
        private const float AngleToleranceBeforeRay = 15f;

        private bool cachedLos;
        private Vector3 lastFirePos;
        private Vector3 lastTargetPos;
        private int losFrame = -1;

        /// <summary>
        /// Whether the fire-point has a clear shot at the target. Targets beyond the angle
        /// tolerance short-circuit to false (you can't fire at them anyway, so skip the raycast).
        /// </summary>
        public bool IsClear(Vector3 firePos, Vector3 targetPos, float angleToTarget)
        {
            if (angleToTarget > AngleToleranceBeforeRay)
                return false;

            var frame = Time.frameCount;
            var needsUpdate = losFrame < 0 || frame - losFrame >= CacheFrames
                                           || Vector3.Distance(firePos, lastFirePos) > 1f
                                           || Vector3.Distance(targetPos, lastTargetPos) > 1f;

            if (!needsUpdate)
                return cachedLos;

            cachedLos = TargetingMath.IsLineClear(firePos, targetPos);
            losFrame = frame;
            lastFirePos = firePos;
            lastTargetPos = targetPos;
            return cachedLos;
        }
    }
}

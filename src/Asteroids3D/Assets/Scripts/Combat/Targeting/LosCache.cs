using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Sim-time-coherent line-of-sight check for a single fire-point. Holds the only mutable state
    /// in targeting: the last raycast result and when/where it was taken, so a weapon polling LOS
    /// every tick doesn't raycast every tick. One per fire-point (not per ship), since the cache
    /// keys on the firing position.
    /// </summary>
    public sealed class LosCache
    {
        // Sim-time TTL, never frames: frame-keyed reuse tied firing behavior to timescale (at 20x, 5 frames spanned ~2 sim-seconds of stale LOS).
        private const float CacheSeconds = 0.1f;

        /// <summary>Aim error (degrees) beyond which we skip the raycast — you can't fire there anyway.</summary>
        private const float AngleToleranceBeforeRay = 15f;

        private bool cachedLos;
        private Vector3 lastFirePos;
        private Vector3 lastTargetPos;
        private float losTime = float.NegativeInfinity;

        /// <summary>
        /// Whether the fire-point has a clear shot at the target. Targets beyond the angle
        /// tolerance short-circuit to false (you can't fire at them anyway, so skip the raycast).
        /// </summary>
        public bool IsClear(Vector3 firePos, Vector3 targetPos, float angleToTarget)
        {
            if (angleToTarget > AngleToleranceBeforeRay)
                return false;

            var now = Time.fixedTime;
            var needsUpdate = now - losTime >= CacheSeconds
                              || Vector3.Distance(firePos, lastFirePos) > 1f
                              || Vector3.Distance(targetPos, lastTargetPos) > 1f;

            if (!needsUpdate)
                return cachedLos;

            cachedLos = TargetingMath.IsLineClear(firePos, targetPos);
            losTime = now;
            lastFirePos = firePos;
            lastTargetPos = targetPos;
            return cachedLos;
        }
    }
}

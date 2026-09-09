using UnityEngine;

namespace Combat.Targeting
{
    /// <summary>Sim-time-cached LOS result per fire-point (the cache keys on firing position, hence not per-ship), so per-tick LOS polling doesn't raycast per tick.</summary>
    public sealed class LosCache
    {
        // TTL in sim seconds, never frames — frame-keyed reuse would scale stale-LOS duration with timescale.
        private const float CacheSeconds = 0.1f;

        /// <summary>Aim error (degrees) beyond which we skip the raycast — you can't fire there anyway.</summary>
        private const float AngleToleranceBeforeRay = 15f;

        private bool cachedLos;
        private Vector3 lastFirePos;
        private Vector3 lastTargetPos;
        private float losTime = float.NegativeInfinity;

        /// <summary>Whether the fire-point has a clear shot at the target; aim errors beyond <see cref="AngleToleranceBeforeRay"/> short-circuit to false.</summary>
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

using UnityEngine;

namespace Utils
{
    /// <summary>
    /// Lightweight helper that answers “is there a clear line-of-sight?” between two points
    /// using a single non-alloc raycast.
    /// </summary>
    public static class LineOfSight
    {
        // Re-use a single-element buffer to avoid per-call allocations (Physics.RaycastNonAlloc).
        private static readonly RaycastHit[] RayBuffer = new RaycastHit[1];

        /// <summary>
        /// Returns true when nothing on <paramref name="occluderMask"/> blocks the ray from
        /// <paramref name="origin"/> to <paramref name="targetPos"/>.
        /// If <paramref name="targetRoot"/> is supplied, a hit on any of its child colliders
        /// is treated as transparent so the target itself does not count as an occluder.
        /// </summary>
        public static bool IsClear(
            Vector3 origin,
            Vector3 targetPos,
            Transform targetRoot = null,
            LayerMask? occluderMask = null)
        {
            Vector3 dir  = targetPos - origin;
            float   dist = dir.magnitude;

            // Degenerate case – same point.
            if (dist <= 0f) return true;

            dir /= dist; // Normalise direction.

            LayerMask mask = occluderMask ?? Physics.DefaultRaycastLayers;

            int hitCount = Physics.RaycastNonAlloc(
                origin,
                dir,
                RayBuffer,
                dist,
                mask,
                QueryTriggerInteraction.Ignore);

            if (hitCount == 0)
            {
                // Nothing intersected up to the target distance – clear LOS.
                return true;
            }

            // If we hit something, LOS is clear only if it belongs to the target root.
            return targetRoot != null && RayBuffer[0].collider.transform.IsChildOf(targetRoot);
        }

        /// <summary>
        /// Overload for the common case where we don't care about "hitting the target" being
        /// special – any hit means the LOS is blocked.
        /// </summary>
        public static bool IsClear(
            Vector3 origin,
            Vector3 targetPos,
            LayerMask? occluderMask = null)
        {
            return IsClear(origin, targetPos, null, occluderMask);
        }
    }
} 
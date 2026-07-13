using UnityEngine;

namespace Utils
{
    public static class LineOfSight
    {
        /// <summary>
        /// True when nothing on <paramref name="occluderMask"/> blocks the ray from
        /// <paramref name="origin"/> to <paramref name="targetPos"/>; a closest hit under
        /// <paramref name="targetRoot"/> is the target itself, not an occluder.
        /// </summary>
        public static bool IsClear(
            Vector3 origin,
            Vector3 targetPos,
            Transform targetRoot = null,
            LayerMask? occluderMask = null)
        {
            var dir  = targetPos - origin;
            var dist = dir.magnitude;
            if (dist <= 0f) return true;

            dir /= dist;
            var mask = occluderMask ?? Physics.DefaultRaycastLayers;

            // Physics.Raycast returns the CLOSEST hit; an unordered NonAlloc query could return the target's own collider while an occluder sits in front.
            if (!Physics.Raycast(origin, dir, out var hit, dist, mask, QueryTriggerInteraction.Ignore))
                return true;

            return targetRoot && hit.collider.transform.IsChildOf(targetRoot);
        }

        public static bool IsClear(
            Vector3 origin,
            Vector3 targetPos,
            LayerMask? occluderMask = null)
        {
            return IsClear(origin, targetPos, null, occluderMask);
        }
    }
}

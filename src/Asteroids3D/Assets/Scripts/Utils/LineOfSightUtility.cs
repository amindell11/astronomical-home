using UnityEngine;

/// <summary>
/// Static helper methods for performing line-of-sight (LOS) checks that avoid
/// per-call allocations and can be reused across gameplay systems.
/// </summary>
public static class LineOfSightUtility
{
    // Single-element buffer reused for non-allocating raycasts.
    private static readonly RaycastHit[] RayBuffer = new RaycastHit[1];

    /// <summary>
    /// Returns <c>true</c> if an unobstructed line of sight exists from
    /// <paramref name="origin"/> to the <paramref name="targetPos"/>.
    /// </summary>
    /// <param name="origin">World-space start point of the ray.</param>
    /// <param name="targetPos">World-space end point (target position).</param>
    /// <param name="targetRoot">
    /// Optional root transform for the target. If supplied, a hit on any child
    /// of this transform is considered a clear LOS.
    /// </param>
    /// <param name="occluderMask">
    /// Layer mask defining which colliders can block the LOS. Defaults to
    /// <see cref="Physics.DefaultRaycastLayers"/>.
    /// </param>
    public static bool HasLineOfSight(
        Vector3 origin,
        Vector3 targetPos,
        Transform targetRoot = null,
        LayerMask occluderMask = Physics.DefaultRaycastLayers)
    {
        Vector3 dir  = targetPos - origin;
        float   dist = dir.magnitude;

        // Degenerate case – zero length.
        if (dist <= 0f) return true;

        dir /= dist; // Normalize.

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            dir,
            RayBuffer,
            dist,
            occluderMask,
            QueryTriggerInteraction.Ignore);

        if (hitCount == 0)
        {
            // Nothing intersected up to the target distance – clear LOS.
            return true;
        }

        // If we did hit something, LOS is clear only if the first hit is the target.
        return targetRoot != null && RayBuffer[0].collider.transform.IsChildOf(targetRoot);
    }

    /// <summary>
    /// Convenience overload that works directly with a <see cref="Transform"/>,
    /// calling the root-aware method internally.
    /// </summary>
    public static bool HasLineOfSight(
        Vector3 origin,
        Transform target,
        LayerMask occluderMask = Physics.DefaultRaycastLayers)
    {
        if (target == null) return false;
        return HasLineOfSight(origin, target.position, target.root, occluderMask);
    }
} 
using UnityEngine;

namespace Cameras
{
    /// <summary>
    /// Pure math utilities for camera calculations. EditMode testable.
    /// </summary>
    public static class CameraUtils
    {
        /// <summary>
        /// Computes the center point of an axis-aligned bounding box.
        /// </summary>
        public static Vector2 BoundsCenter(Vector2 min, Vector2 max) => (min + max) * 0.5f;

        /// <summary>
        /// Computes the orthographic size required to fit bounds around a center point.
        /// Returns the size needed to show all content with padding, respecting aspect ratio.
        /// </summary>
        public static float ZoomToFitBounds(
            Vector2 center, Vector2 boundsMin, Vector2 boundsMax, 
            float padding, float aspect)
        {
            var maxDx = Mathf.Max(center.x - boundsMin.x, boundsMax.x - center.x);
            var maxDy = Mathf.Max(center.y - boundsMin.y, boundsMax.y - center.y);
            return Mathf.Max(maxDy + padding, (maxDx + padding) / aspect);
        }

        /// <summary>
        /// Shifts a camera center to keep a target point within view bounds.
        /// Returns the adjusted center position.
        /// </summary>
        public static Vector2 ShiftToKeepPointInView(
            Vector2 center, Vector2 targetOffset, 
            float zoomSize, float aspect, float padding)
        {
            var horizontalExtent = zoomSize * aspect;
            var verticalExtent = zoomSize;

            if (Mathf.Abs(targetOffset.x) > horizontalExtent - padding)
            {
                var shiftX = Mathf.Abs(targetOffset.x) - (horizontalExtent - padding);
                center.x += Mathf.Sign(targetOffset.x) * shiftX;
            }
            if (Mathf.Abs(targetOffset.y) > verticalExtent - padding)
            {
                var shiftY = Mathf.Abs(targetOffset.y) - (verticalExtent - padding);
                center.y += Mathf.Sign(targetOffset.y) * shiftY;
            }

            return center;
        }

        /// <summary>
        /// Expands bounds to include a new point.
        /// </summary>
        public static void ExpandBounds(ref Vector2 min, ref Vector2 max, Vector2 point)
        {
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        /// <summary>
        /// Initializes bounds to empty state (ready for expansion).
        /// </summary>
        public static void InitEmptyBounds(out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        }

        /// <summary>
        /// Returns true if bounds are valid (have been expanded at least once).
        /// </summary>
        public static bool AreBoundsValid(Vector2 min, Vector2 max) =>
            min.x <= max.x && min.y <= max.y;
    }
}

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

        public static Vector2 ShiftToFitBounds(Vector2 center, Vector2 boundsMin, Vector2 boundsMax,
            float zoomSize, float aspect, float padding) =>
            new(ClampIntoReach(center.x, boundsMin.x, boundsMax.x, zoomSize * aspect - padding),
                ClampIntoReach(center.y, boundsMin.y, boundsMax.y, zoomSize - padding));

        private static float ClampIntoReach(float center, float min, float max, float reach) =>
            max - reach > min + reach ? (min + max) * 0.5f : Mathf.Clamp(center, max - reach, min + reach);

        public static float SmoothZoom(float current, float target, ref float velocity,
            float widenTime, float narrowTime, float deltaTime)
        {
            // Momentum built toward an earlier target must not carry the zoom away from this one.
            if (velocity * (target - current) <= 0f)
                velocity = 0f;
            var smoothTime = target > current ? widenTime : narrowTime;
            return Mathf.SmoothDamp(current, target, ref velocity, smoothTime, float.PositiveInfinity, deltaTime);
        }

        public static Vector2 CursorLead(Vector2 viewportPoint, float deadZone, float distance)
        {
            if (viewportPoint.x < 0f || viewportPoint.x > 1f || viewportPoint.y < 0f || viewportPoint.y > 1f)
                return Vector2.zero;
            var offset = Vector2.ClampMagnitude(viewportPoint * 2f - Vector2.one, 1f);
            return offset.normalized * (Mathf.InverseLerp(deadZone, 1f, offset.magnitude) * distance);
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

        // Non-integer seeds: Perlin noise is flat at lattice points.
        public static float ShakeNoise(int channel, float time, float frequency) =>
            Mathf.Clamp(Mathf.PerlinNoise(channel * 17.31f + .5f, time * frequency) * 2f - 1f, -1f, 1f);

        public static Matrix4x4 ShakenView(Matrix4x4 view, Vector2 offset, float rollDegrees) =>
            Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, -rollDegrees))
            * Matrix4x4.Translate(new Vector3(-offset.x, -offset.y, 0f))
            * view;

        public static void CapShake(ref Vector2 offset, ref float roll, float maxOffset, float maxRoll)
        {
            offset = Vector2.ClampMagnitude(offset, maxOffset);
            roll = Mathf.Clamp(roll, -maxRoll, maxRoll);
        }

        public static float BlastStrength(float distance, float innerRadius, float outerRadius, float size, float fullSize)
        {
            var scale = fullSize > 0f ? Mathf.Clamp01(size / fullSize) : 1f;
            var inner = innerRadius * scale;
            var outer = outerRadius * scale;
            if (distance <= inner) return scale;
            if (distance >= outer) return 0f;
            return scale * (1f - Mathf.InverseLerp(inner, outer, distance));
        }
    }
}

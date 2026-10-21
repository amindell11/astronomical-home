using UnityEngine;

namespace Tests.PlayMode.Common
{

/// <summary>
/// General-purpose test utilities for PlayMode tests.
/// Provides common helper methods to reduce code duplication.
/// </summary>
public static class TestUtilities
{
    /// <summary>
    /// Gets the 2D facing angle of a transform on the game plane.
    /// </summary>
    /// <param name="transform">Transform to measure</param>
    /// <returns>Signed angle in degrees relative to plane up (0° = forward)</returns>
    public static float GetPlaneFacingAngle(Transform transform)
    {
        return Vector2.SignedAngle(Vector2.up, transform.up);
    }

    /// <summary>
    /// Calculates the angular difference between a transform's facing and a target angle.
    /// </summary>
    /// <param name="transform">Transform to measure</param>
    /// <param name="targetAngle">Target angle in degrees</param>
    /// <returns>Shortest angular difference in degrees</returns>
    public static float AngleDeltaToTarget(Transform transform, float targetAngle)
    {
        var facingAngle = GetPlaneFacingAngle(transform);
        return Mathf.Abs(Mathf.DeltaAngle(facingAngle, targetAngle));
    }
}

} // namespace Tests.PlayMode.Common

using Game;
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
    /// Calculates the 2D plane distance between a transform and a target point.
    /// </summary>
    /// <param name="transform">Transform to measure from</param>
    /// <param name="target">Target position on the game plane</param>
    /// <returns>Distance in plane coordinates</returns>
    public static float DistanceToPlaneTarget(Transform transform, Vector2 target)
    {
        var pos2D = GamePlane.WorldPointToPlane(transform.position);
        return Vector2.Distance(pos2D, target);
    }

    /// <summary>
    /// Calculates the 2D plane distance between a world position and a target point.
    /// </summary>
    /// <param name="worldPosition">World position to measure from</param>
    /// <param name="target">Target position on the game plane</param>
    /// <returns>Distance in plane coordinates</returns>
    public static float DistanceToPlaneTarget(Vector3 worldPosition, Vector2 target)
    {
        var pos2D = GamePlane.WorldPointToPlane(worldPosition);
        return Vector2.Distance(pos2D, target);
    }

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

    /// <summary>
    /// Pauses the AudioListener (useful in SetUp to prevent audio spam in tests).
    /// </summary>
    public static void PauseAudio()
    {
        AudioListener.pause = true;
    }

    /// <summary>
    /// Resumes the AudioListener (useful in TearDown).
    /// </summary>
    public static void ResumeAudio()
    {
        AudioListener.pause = false;
    }
}

} // namespace Tests.PlayMode.Common

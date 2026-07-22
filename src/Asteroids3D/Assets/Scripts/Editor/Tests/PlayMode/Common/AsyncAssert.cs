using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace Tests.PlayMode.Common
{

/// <summary>
/// Async assertion utilities for PlayMode tests.
/// Provides polling-based assertions with configurable timeouts to reduce test duplication.
/// </summary>
public static class AsyncAssert
{
    /// <summary>
    /// Polls a condition until it becomes true or times out.
    /// Yields on each frame to allow Unity updates.
    /// </summary>
    /// <param name="condition">Condition to poll (should return true when satisfied)</param>
    /// <param name="timeoutSec">Timeout in seconds</param>
    /// <param name="failureMessage">Custom assertion message on timeout</param>
    /// <param name="useFixedUpdate">If true, uses WaitForFixedUpdate; otherwise yields null (default)</param>
    /// <returns>IEnumerator for use in UnityTest</returns>
    public static IEnumerator WaitUntil(
        Func<bool> condition,
        float timeoutSec,
        string failureMessage = "Condition was not satisfied within timeout",
        bool useFixedUpdate = false)
    {
        var deadline = Time.realtimeSinceStartup + timeoutSec;
        
        while (Time.realtimeSinceStartup < deadline)
        {
            if (condition())
            {
                Assert.Pass();
                yield break;
            }

            if (useFixedUpdate)
                yield return new WaitForFixedUpdate();
            else
                yield return null;
        }

        Assert.Fail($"{failureMessage} (timeout: {timeoutSec}s)");
    }

    /// <summary>
    /// Polls a condition until it becomes true or times out, then performs a final assertion.
    /// This variant allows early exit on success but still runs a custom assertion afterward.
    /// </summary>
    /// <param name="condition">Condition to poll (should return true when satisfied)</param>
    /// <param name="timeoutSec">Timeout in seconds</param>
    /// <param name="finalAssertion">Final assertion to run after polling completes</param>
    /// <param name="useFixedUpdate">If true, uses WaitForFixedUpdate; otherwise yields null (default)</param>
    /// <returns>IEnumerator for use in UnityTest</returns>
    public static IEnumerator WaitUntilThen(
        Func<bool> condition,
        float timeoutSec,
        Action finalAssertion,
        bool useFixedUpdate = false)
    {
        var deadline = Time.realtimeSinceStartup + timeoutSec;
        
        while (Time.realtimeSinceStartup < deadline)
        {
            if (condition())
                break;

            if (useFixedUpdate)
                yield return new WaitForFixedUpdate();
            else
                yield return null;
        }

        finalAssertion?.Invoke();
    }

    /// <summary>
    /// Frame-counted negative proof for frame-driven behaviour (camera follow, scanning):
    /// N frames of non-occurrence prove as much as a wall-clock wait at a fraction of the cost.
    /// </summary>
    public static IEnumerator AssertRemainsFalseForFrames(
        Func<bool> condition,
        int frames,
        string failureMessage = "Condition unexpectedly became true")
    {
        for (var i = 0; i < frames; i++)
        {
            if (condition())
                Assert.Fail(failureMessage);
            yield return null;
        }
    }

    /// <summary>
    /// Polls until a float value is within tolerance of a target, or times out.
    /// </summary>
    /// <param name="getValue">Function that returns the current value</param>
    /// <param name="target">Target value</param>
    /// <param name="tolerance">Acceptable tolerance</param>
    /// <param name="timeoutSec">Timeout in seconds</param>
    /// <param name="failureMessage">Custom failure message</param>
    /// <param name="useFixedUpdate">If true, uses WaitForFixedUpdate; otherwise yields null (default)</param>
    /// <returns>IEnumerator for use in UnityTest</returns>
    public static IEnumerator WaitForFloatWithinTolerance(
        Func<float> getValue,
        float target,
        float tolerance,
        float timeoutSec,
        string failureMessage = "Value did not reach target within timeout",
        bool useFixedUpdate = false)
    {
        return WaitUntilThen(
            () => Mathf.Abs(getValue() - target) < tolerance,
            timeoutSec,
            () => Assert.That(getValue(), Is.EqualTo(target).Within(tolerance), failureMessage),
            useFixedUpdate);
    }

    /// <summary>
    /// Polls until a Vector2 distance is within threshold of target, or times out.
    /// </summary>
    /// <param name="getPosition">Function that returns the current position</param>
    /// <param name="target">Target position</param>
    /// <param name="threshold">Distance threshold</param>
    /// <param name="timeoutSec">Timeout in seconds</param>
    /// <param name="failureMessage">Custom failure message</param>
    /// <param name="useFixedUpdate">If true, uses WaitForFixedUpdate; otherwise yields null (default)</param>
    /// <returns>IEnumerator for use in UnityTest</returns>
    public static IEnumerator WaitForVector2NearTarget(
        Func<Vector2> getPosition,
        Vector2 target,
        float threshold,
        float timeoutSec,
        string failureMessage = "Position did not reach target within timeout",
        bool useFixedUpdate = false)
    {
        return WaitUntilThen(
            () => Vector2.Distance(getPosition(), target) < threshold,
            timeoutSec,
            () => Assert.That(Vector2.Distance(getPosition(), target), Is.LessThan(threshold), failureMessage),
            useFixedUpdate);
    }
}

} // namespace Tests.PlayMode.Common

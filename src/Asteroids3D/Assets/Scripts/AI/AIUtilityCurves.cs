using UnityEngine;

/// <summary>
/// A collection of static methods for calculating utility scores based on smooth curves.
/// These replace hard-coded thresholds with more nuanced, continuous functions.
/// </summary>
public static class AIUtilityCurves
{
    /// <summary>
    /// A smooth utility curve that returns a high score when the input value is high.
    /// This is useful for "desire" behaviors, like attacking when health is high.
    /// The curve is a standard smoothstep function.
    /// </summary>
    /// <param name="value">The input value to evaluate (e.g., HealthPct), clamped to [0, 1].</param>
    /// <param name="maxBonus">The maximum utility bonus this curve can return.</param>
    /// <returns>A utility score between 0 and maxBonus.</returns>
    public static float DesireCurve(float value, float maxBonus)
    {
        value = Mathf.Clamp01(value);
        // Standard SmoothStep: f(t) = 3t^2 - 2t^3, which is t*t*(3-2*t)
        float t = value * value * (3f - 2f * value);
        return t * maxBonus;
    }

    /// <summary>
    /// A smooth utility curve that returns a high score when the input value is low.
    /// This is useful for "fear" behaviors, like evading when health is low.
    /// The curve is an inverted smoothstep function.
    /// </summary>
    /// <param name="value">The input value to evaluate (e.g., HealthPct), clamped to [0, 1].</param>
    /// <param name="maxBonus">The maximum utility bonus this curve can return.</param>
    /// <returns>A utility score between 0 and maxBonus.</returns>
    public static float FearCurve(float value, float maxBonus)
    {
        value = Mathf.Clamp01(value);
        // Inverted SmoothStep: 1 - f(t)
        float t = value * value * (3f - 2f * value);
        return (1f - t) * maxBonus;
    }
} 
using UnityEngine;

/// <summary>
/// Per-instance tuning parameters for steering algorithms.  By passing this
/// struct to <see cref="VelocityPilot"/> or other planners, callers can
/// customise acceleration and dead-zone values on a per-ship (or per-agent)
/// basis instead of relying on global constants.
/// </summary>
public readonly struct SteeringTuning
{
    public readonly float ForwardAcc;
    public readonly float ReverseAcc;
    public readonly float StrafeAcc;
    public readonly float DeadZone;

    public SteeringTuning(float forwardAcc, float reverseAcc, float strafeAcc, float deadZone)
    {
        ForwardAcc  = forwardAcc;
        ReverseAcc  = reverseAcc;
        StrafeAcc   = strafeAcc;
        DeadZone    = deadZone;
    }

    /// <summary>
    /// Default tuning values, used as a fallback when per-ship settings are not available.
    /// </summary>
    public static readonly SteeringTuning Default = new SteeringTuning(
        forwardAcc: 8f,
        reverseAcc: 4f,
        strafeAcc:  6f,
        deadZone:   0.1f);
} 
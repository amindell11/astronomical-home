using UnityEngine;

public static class VelocityPilot
{
    // --- Tunable Parameters ---
    [Header("Tuning")]
    [Tooltip("Maximum forward acceleration in m/s^2.")]
    public static float ForwardAcceleration = 8.0f;

    [Tooltip("Maximum reverse acceleration in m/s^2.")]
    public static float ReverseAcceleration = 4.0f;

    [Tooltip("Maximum strafe acceleration in m/s^2.")]
    public static float StrafeAcceleration = 6.0f;

    [Tooltip("The dead zone for velocity errors, below which no thrust is applied.")]
    public static float VelocityDeadZone = 0.1f;
    
    // Note: MaxSpeed is read from the Ship component itself.

    /// <summary>
    /// Computes thrust, strafe, and rotation commands to guide a ship to a waypoint.
    /// </summary>
    /// <param name="waypoint">Target position in 2D plane space.</param>
    /// <param name="currentPosition">Current ship position in 2D plane space.</param>
    /// <param name="currentVelocity">Current ship velocity in 2D plane space.</param>
    /// <param name="currentForward">Current ship forward direction (normalized) in 2D plane space.</param>
    /// <param name="maxSpeed">The maximum speed the ship can travel.</param>
    /// <param name="thrustCmd">Normalized forward/reverse thrust command [-1, 1].</param>
    /// <param name="strafeCmd">Normalized strafe command [-1, 1].</param>
    /// <param name="rotTargetDeg">The desired heading in degrees [0, 360].</param>
    public static void ComputeInputs(
        Vector2 waypoint, Vector2 currentPosition, Vector2 currentVelocity, Vector2 currentForward, float maxSpeed,
        out float thrustCmd, out float strafeCmd, out float rotTargetDeg)
    {
        // 1. Compute Desired Velocity
        Vector2 vectorToWaypoint = waypoint - currentPosition;
        float distanceToWaypoint = vectorToWaypoint.magnitude;
        Vector2 directionToWaypoint = (distanceToWaypoint > 0.01f) ? vectorToWaypoint.normalized : Vector2.zero;

        // Calculate the maximum speed we can have to be able to stop at the waypoint.
        // v_final^2 = v_initial^2 + 2ad => v_initial = sqrt(-2ad) -- but d is negative
        // More simply: E_k = W_done_by_braking => 0.5*m*v^2 = F*d => v = sqrt(2*a*d)
        float maxSpeedToStop = Mathf.Sqrt(2 * ForwardAcceleration * distanceToWaypoint);
        float desiredSpeed = Mathf.Min(maxSpeed, maxSpeedToStop);
        Vector2 desiredVelocity = directionToWaypoint * desiredSpeed;

        // 2. Find Velocity Error
        Vector2 velocityError = desiredVelocity - currentVelocity;

        // 3. Project Error onto Ship's Axes
        Vector2 shipRight = new Vector2(currentForward.y, -currentForward.x);
        float forwardError = Vector2.Dot(velocityError, currentForward);
        float strafeError = Vector2.Dot(velocityError, shipRight);
        
        // 4. Map Errors to Commands
        if (forwardError > 0)
        {
            // Need to accelerate forward
            thrustCmd = forwardError / ForwardAcceleration;
        }
        else
        {
            // Need to brake or reverse
            thrustCmd = forwardError / ReverseAcceleration; // forwardError is negative
        }

        strafeCmd = strafeError / StrafeAcceleration;

        // 5. Apply Dead Zone and Clamp
        if (velocityError.magnitude < VelocityDeadZone)
        {
            thrustCmd = 0f;
            strafeCmd = 0f;
        }
        else
        {
            thrustCmd = Mathf.Clamp(thrustCmd, -1f, 1f);
            strafeCmd = Mathf.Clamp(strafeCmd, -1f, 1f);
        }

        // --- Rotation ---
        // We want to point the ship towards the desired velocity vector.
        Vector2 targetDirection = desiredVelocity.normalized;
        
        // If we are very close to the desired velocity, just point towards the waypoint.
        if (desiredVelocity.magnitude < 0.5f)
        {
            targetDirection = directionToWaypoint;
        }

        if (targetDirection.sqrMagnitude > 0.01f)
        {
            // The ship's angle is measured counter-clockwise from Vector2.up.
            rotTargetDeg = Vector2.SignedAngle(Vector2.up, targetDirection);
            if (rotTargetDeg < 0) rotTargetDeg += 360f;
        }
        else
        {
            // If there's no direction, maintain current heading.
            rotTargetDeg = Vector2.SignedAngle(Vector2.up, currentForward);
            if (rotTargetDeg < 0) rotTargetDeg += 360f;
        }
    }
}

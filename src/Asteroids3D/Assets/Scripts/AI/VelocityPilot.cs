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

    // --- New typed IO structs for modular pipeline ---------------------------
    public readonly struct Input
    {
        public readonly ShipKinematics kin;
        public readonly Vector2 waypoint;   // desired point in plane space
        public readonly Vector2 desiredVel; // OR you can pass desiredVel directly
        public readonly Vector2 waypointVel; // desired velocity at waypoint (for velocity matching)
        public readonly float   maxSpeed;

        public Input(ShipKinematics k, Vector2 wp, Vector2 desiredVelocity, Vector2 wpVelocity, float max)
        {
            kin = k;
            waypoint = wp;
            desiredVel = desiredVelocity;
            waypointVel = wpVelocity;
            maxSpeed = max;
        }
    }

    public readonly struct Output
    {
        public readonly float thrust;
        public readonly float strafe;
        public readonly float rotTargetDeg;

        public Output(float t, float s, float r)
        {
            thrust = t; strafe = s; rotTargetDeg = r;
        }
    }

    /// <summary>
    /// New modular Compute entry point that maps desired velocity to control commands.
    /// Internally reuses the legacy ComputeInputs for now so existing behaviour remains.
    /// </summary>
    public static Output Compute(Input i)
    {
        // Use legacy API but build necessary parameters
        // If desiredVel is zero, fall back to waypoint logic like old ComputeInputs.

        Vector2 desired = i.desiredVel;
        Vector2 curPos  = i.kin.Pos;
        Vector2 curVel  = i.kin.Vel;
        Vector2 forward = i.kin.Forward;

        float thrust, strafe, rot;

        if (desired != Vector2.zero)
        {
            // Convert desired velocity into an implicit waypoint step ahead.
            Vector2 wp = curPos + desired * 0.2f; // 0.2 s lead – arbitrary, tweak later
            ComputeInputs(wp, curPos, curVel, forward, i.maxSpeed, i.waypointVel, out thrust, out strafe, out rot);
        }
        else
        {
            ComputeInputs(i.waypoint, curPos, curVel, forward, i.maxSpeed, i.waypointVel, out thrust, out strafe, out rot);
        }

        return new Output(thrust, strafe, rot);
    }

    /// <summary>
    /// Computes thrust, strafe, and rotation commands to guide a ship to a waypoint.
    /// </summary>
    /// <param name="waypoint">Target position in 2D plane space.</param>
    /// <param name="currentPosition">Current ship position in 2D plane space.</param>
    /// <param name="currentVelocity">Current ship velocity in 2D plane space.</param>
    /// <param name="currentForward">Current ship forward direction (normalized) in 2D plane space.</param>
    /// <param name="maxSpeed">The maximum speed the ship can travel.</param>
    /// <param name="waypointVel">Desired velocity at waypoint (for velocity matching).</param>
    /// <param name="thrustCmd">Normalized forward/reverse thrust command [-1, 1].</param>
    /// <param name="strafeCmd">Normalized strafe command [-1, 1].</param>
    /// <param name="rotTargetDeg">The desired heading in degrees [0, 360].</param>
    public static void ComputeInputs(
        Vector2 waypoint, Vector2 currentPosition, Vector2 currentVelocity, Vector2 currentForward, float maxSpeed, Vector2 waypointVel,
        out float thrustCmd, out float strafeCmd, out float rotTargetDeg)
    {
        // 1. Compute Desired Velocity taking waypoint velocity into account
        Vector2 vectorToWaypoint = waypoint - currentPosition;
        float   distanceToWaypoint = vectorToWaypoint.magnitude;
        Vector2 directionToWaypoint = distanceToWaypoint > 0.01f ? vectorToWaypoint.normalized : Vector2.zero;

        // Maximum relative speed we can still lose over remaining distance with max decel
        float maxRelativeSpeed = Mathf.Sqrt(2f * ForwardAcceleration * distanceToWaypoint);

        // Desired relative speed (clamped to ship max)
        float desiredRelSpeed = Mathf.Min(maxRelativeSpeed, maxSpeed);

        // Desired world velocity is waypoint velocity plus allowed relative component along path
        Vector2 desiredVelocity = waypointVel + directionToWaypoint * desiredRelSpeed;

        // Ensure we do not exceed absolute max speed
        if (desiredVelocity.sqrMagnitude > maxSpeed * maxSpeed)
            desiredVelocity = desiredVelocity.normalized * maxSpeed;

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

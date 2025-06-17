using System.Collections.Generic;
using UnityEngine;

/// Stateless utility that turns a goal + local obstacles into
///   (thrust, strafe, rotationTargetDeg) commands.
///
/// * NO references to Ship / Rigidbody / MonoBehaviour.
/// * Call every FixedUpdate from any AI script.
public static class PathPlanner
{
    public struct Input
    {
        public Vector3 position;           // world
        public Vector3 velocity;           // world
        public float   radius;             // ship collider radius
        public Vector3 forward;            // world-space forward (+Y in 2-D plane)
        public Transform goal;             // waypoint / target
        public float   maxSpeed;
        public float   arriveRadius;

        // avoidance
        public float lookAheadTime;
        public float safeMargin;
        public IReadOnlyList<Collider> nearbyAsteroids;   // already filtered
    }

    public struct Output
    {
        public float thrust;          // −1..+1 (forward / reverse)
        public float strafe;          // −1..+1 (left / right)
        public float rotTargetDeg;    // absolute plane-angle to face
    }

    /// <summary>
    /// Optional debug data that can be requested from <see cref="Compute(PathPlanner.Input, out DebugInfo)"/>.
    /// </summary>
    public struct DebugInfo
    {
        public Vector3 future;           // predicted future position of the ship
        public Vector3 desired;          // desired velocity towards goal (seek/arrive)
        public Vector3 avoid;            // avoidance velocity contribution
        public Vector3 accel;            // resulting acceleration vector (desired+avoid - current vel)

        /// <summary>
        /// Future positions of asteroids that were considered colliding (within combined radius).
        /// </summary>
        public List<Vector3> rockFutures;
    }

    /* ── Tuning parameters (same defaults as VelocityPilot) ─────────────── */
    public static float ForwardAcceleration = 8f;
    public static float ReverseAcceleration = 4f;
    public static float StrafeAcceleration  = 6f;
    public static float VelocityDeadZone    = 0.1f;

    public static Output Compute(Input io)
    {
        return Compute(io, out _); // discard debug
    }

    /// <summary>
    /// Core path-planning routine. When called with <paramref name="dbg"/>, the method also fills
    /// out useful intermediate data that can be visualised via gizmos for debugging and teaching.
    /// </summary>
    public static Output Compute(Input io, out DebugInfo dbg)
    {
        /* -------- seek / arrive --------------------------------- */
        Vector3 toGoal   = (io.goal.position - io.position);
        float   dist     = toGoal.magnitude;
        float   tgtSpeed = dist > io.arriveRadius
                           ? io.maxSpeed
                           : Mathf.Lerp(0, io.maxSpeed, dist / io.arriveRadius);
        Vector3 desired  = toGoal.normalized * tgtSpeed;

        /* -------- predictive avoidance --------------------------- */
        Vector3 future   = io.position + io.velocity * io.lookAheadTime;
        Vector3 push     = Vector3.zero;
        float   weightT  = 0f;

        List<Vector3> collidingFutures = null;

        Vector3 segStart = io.position;
        Vector3 segEnd   = future;
        Vector3 segDir   = segEnd - segStart;
        float   segLenSq = segDir.sqrMagnitude;

        foreach (var rock in io.nearbyAsteroids)
        {
            Vector3 rockPos = rock.transform.position;
            Vector3 rockVel = rock.attachedRigidbody ? rock.attachedRigidbody.velocity : Vector3.zero;
            Vector3 rockFut = rockPos + rockVel * io.lookAheadTime;

            float rockRad = rock.bounds.extents.x;
            float combined = io.radius + rockRad + io.safeMargin;

            // Closest point on ship segment to rockFut
            float t = 0f;
            Vector3 offset = rockFut - segStart;
            if (segLenSq > 0.0001f)
            {
                t = Mathf.Clamp(Vector3.Dot(offset, segDir) / segLenSq, 0f, 1f);
            }
            Vector3 closest = segStart + segDir * t;
            Vector3 sep     = closest - rockFut;
            float sq = sep.sqrMagnitude;

            if (sq < combined * combined)
            {
                float w = 1f / Mathf.Max(sq, 0.01f);
                push    += sep.normalized * w;
                weightT += w;

                collidingFutures ??= new List<Vector3>();
                collidingFutures.Add(rockFut);
            }
        }
        Vector3 avoid = (weightT > 0) ? push / weightT * io.maxSpeed : Vector3.zero;

        /* -------- velocity-error steering ----------------------- */
        Vector3 desiredVel = desired + avoid;
        Vector3 velError   = desiredVel - io.velocity;

        Vector3 localErr   = Quaternion.Inverse(Quaternion.LookRotation(Vector3.forward, io.forward))
                            * velError; // ship plane: x=right, y=forward

        // Proportional mapping
        float thrustCmd;
        if (localErr.y > 0)
            thrustCmd = localErr.y / ForwardAcceleration;
        else
            thrustCmd = localErr.y / ReverseAcceleration;

        float strafeCmd = localErr.x / StrafeAcceleration;

        if (velError.magnitude < VelocityDeadZone)
        {
            thrustCmd = 0f;
            strafeCmd = 0f;
        }

        Output o;
        o.thrust = Mathf.Clamp(thrustCmd, -1f, 1f);
        o.strafe = Mathf.Clamp(strafeCmd, -1f, 1f);

        // Aim heading toward desired velocity (or toGoal if too slow)
        Vector3 heading = desiredVel.sqrMagnitude > 0.5f ? desiredVel : toGoal;
        o.rotTargetDeg  = Mathf.Atan2(-heading.x, heading.z) * Mathf.Rad2Deg;

        dbg.future      = future;
        dbg.desired     = desired;
        dbg.avoid       = avoid;
        dbg.accel       = velError; // renamed but keep field
        dbg.rockFutures = collidingFutures ?? new List<Vector3>();

        return o;
    }

    /// <summary>
    /// Returns an intermediate steer target (world position) that guides the ship around
    /// obstacles while progressing toward the goal. This is intended for use with
    /// VelocityPilot-style controllers that convert waypoints into thrust/strafe commands.
    /// The debug info is filled the same way as in Compute.
    /// </summary>
    public static Vector3 SteerTarget(Input io, out DebugInfo dbg)
    {
        /* --- reuse seek/avoid logic from Compute up to avoid vector --- */
        Vector3 toGoal   = (io.goal.position - io.position);
        float   dist     = toGoal.magnitude;
        float   tgtSpeed = dist > io.arriveRadius
                           ? io.maxSpeed
                           : Mathf.Lerp(0, io.maxSpeed, dist / io.arriveRadius);
        Vector3 desired  = toGoal.normalized * tgtSpeed;

        Vector3 future   = io.position + io.velocity * io.lookAheadTime;
        Vector3 push     = Vector3.zero;
        float   weightT  = 0f;
        List<Vector3> collidingFutures = null;

        Vector3 segStart = io.position;
        Vector3 segEnd   = future;
        Vector3 segDir   = segEnd - segStart;
        float   segLenSq = segDir.sqrMagnitude;

        foreach (var rock in io.nearbyAsteroids)
        {
            Vector3 rockPos = rock.transform.position;
            Vector3 rockVel = rock.attachedRigidbody ? rock.attachedRigidbody.velocity : Vector3.zero;
            Vector3 rockFut = rockPos + rockVel * io.lookAheadTime;

            float rockRad = rock.bounds.extents.x;
            float combined = io.radius + rockRad + io.safeMargin;

            // Closest point on ship segment to rockFut
            float t = 0f;
            Vector3 offset = rockFut - segStart;
            if (segLenSq > 0.0001f)
            {
                t = Mathf.Clamp(Vector3.Dot(offset, segDir) / segLenSq, 0f, 1f);
            }
            Vector3 closest = segStart + segDir * t;
            Vector3 sep     = closest - rockFut;
            float sq = sep.sqrMagnitude;

            if (sq < combined * combined)
            {
                float w = 1f / Mathf.Max(sq, 0.01f);
                push    += sep.normalized * w;
                weightT += w;

                collidingFutures ??= new List<Vector3>();
                collidingFutures.Add(rockFut);
            }
        }
        Vector3 avoid = (weightT > 0) ? push / weightT * io.maxSpeed : Vector3.zero;

        // ---- choose steer target ----
        Vector3 heading = desired + avoid;
        if (heading.sqrMagnitude < 0.01f) heading = toGoal; // fallback

        // distance ahead along heading to place the steer target
        float step = Mathf.Clamp(io.maxSpeed * io.lookAheadTime * 0.5f, 2f, 20f);
        Vector3 waypoint = io.position + heading.normalized * step;

        dbg.future      = future;
        dbg.desired     = desired;
        dbg.avoid       = avoid;
        dbg.accel       = (desired + avoid) - io.velocity;
        dbg.rockFutures = collidingFutures ?? new List<Vector3>();

        return waypoint;
    }
}

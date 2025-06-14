using UnityEngine;

/// <summary>
/// Utility that converts “where I want to go” into control-surface commands.
/// • thrustCmd  ∈ [-1, 1]  (-1 = full reverse, +1 = full forward)  
/// • strafeCmd  ∈ [-1, 1]  (-1 = push left,     +1 = push right)  
/// • rotTarget° angle in world space for optional gun/heading control
/// </summary>
public static class VelocityPilot
{
    // --- Tunables (expose in inspector or AI profile) ------------------------
    public const float ACCEL_FWD     = 1200f/215f;      // forward m/s²
    public const float ACCEL_REV     = 1200f/215f;      // reverse m/s²
    public const float ACCEL_STRAFE  = 800f/215f;      // lateral m/s²
    public const float MAX_SPEED     = 15f;      // hard cap
    public const float DEAD_ZONE     = 0.25f;    // ignore tiny errors

    /// <param name="waypoint">world-space target position</param>
    /// <param name="pos">current world position</param>
    /// <param name="vel">current world velocity</param>
    /// <param name="fwd">ship nose direction (Transform.right for 2-D)</param>
    /// <param name="enableDebug">whether to output debug logs</param>
    public static void ComputeInputs(
        Vector2 waypoint,
        Vector2 pos,
        Vector2 vel,
        Vector2 fwd,
        Vector2 right,
        out float thrustCmd,
        out float strafeCmd,
        out float rotTargetDeg,
        bool enableDebug = true)
    {
        /* ---------- 1. Desired velocity ----------------------------------- */
        Vector2 toWp = waypoint - pos;
        float dist   = toWp.magnitude;
        Vector2 dir  = toWp / Mathf.Max(dist, 0.001f);        // unit

        // “Brake-safe” target speed: v ≤ √(2 · a · d)
        float tgtSpeed = Mathf.Min(MAX_SPEED, Mathf.Sqrt(2f * ACCEL_FWD * dist));
        Vector2 vDesired = dir * tgtSpeed;
        
        if (enableDebug)
        {
            Debug.Log($"VelocityPilot Step 1 - Desired Velocity:");
            Debug.Log($"  Waypoint: {waypoint}, Current Pos: {pos}");
            Debug.Log($"  Distance: {dist:F2}m, Direction: {dir}");
            Debug.Log($"  Target Speed: {tgtSpeed:F2}m/s (max: {MAX_SPEED}), Desired Velocity: {vDesired}");
        }

        /* ---------- 2. Velocity error ------------------------------------- */
        Vector2 dv = vDesired - vel;
        
        if (enableDebug)
        {
            Debug.Log($"VelocityPilot Step 2 - Velocity Error:");
            Debug.Log($"  Current Velocity: {vel}, Desired Velocity: {vDesired}");
            Debug.Log($"  Velocity Error (dv): {dv}, Magnitude: {dv.magnitude:F2}m/s");
        }

        /* ---------- 3. Project error onto body axes ----------------------- */
        Vector2 right = new Vector2(-fwd.y, fwd.x);           // starboard
        float eLong   = Vector2.Dot(dv, fwd);                 // along nose
        float eLat    = Vector2.Dot(dv, right);               // sideways
        
        if (enableDebug)
        {
            Debug.Log($"VelocityPilot Step 3 - Body Axis Projection:");
            Debug.Log($"  Ship Forward: {fwd}, Ship Right: {right}");
            Debug.Log($"  Longitudinal Error (eLong): {eLong:F2}m/s");
            Debug.Log($"  Lateral Error (eLat): {eLat:F2}m/s");
        }

        /* ---------- 4. Map to control commands --------------------------- */
        // Longitudinal (forward/reverse); scale to ±1
        if (Mathf.Abs(eLong) < DEAD_ZONE)          thrustCmd = 0f;
        else if (eLong > 0f)                       thrustCmd =  Mathf.Clamp01(eLong / ACCEL_FWD);
        else                                       thrustCmd = -Mathf.Clamp01(-eLong / ACCEL_REV);

        // Lateral (strafe)
        if (Mathf.Abs(eLat) < DEAD_ZONE)           strafeCmd = 0f;
        else                                       strafeCmd = Mathf.Clamp(eLat / ACCEL_STRAFE, -1f, 1f);
        
        if (enableDebug)
        {
            Debug.Log($"VelocityPilot Step 4 - Control Commands:");
            Debug.Log($"  Thrust Command: {thrustCmd:F3} (eLong: {eLong:F2}, deadzone: {DEAD_ZONE})");
            Debug.Log($"  Strafe Command: {strafeCmd:F3} (eLat: {eLat:F2}, deadzone: {DEAD_ZONE})");
        }

        /* ---------- 5. Optional rotation target -------------------------- */
        rotTargetDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        
        if (enableDebug)
        {
            Debug.Log($"VelocityPilot Step 5 - Rotation Target:");
            Debug.Log($"  Target Direction: {dir}, Rotation Target: {rotTargetDeg:F1}°");
            Debug.Log($"VelocityPilot FINAL OUTPUT: Thrust={thrustCmd:F3}, Strafe={strafeCmd:F3}, Rotation={rotTargetDeg:F1}°");
        }
    }
}

using UnityEngine;

/// <summary>
/// Immutable snapshot of the ship motion state that is passed through the
/// guidance pipeline (AIShipInput → PathPlanner → VelocityPilot).
/// Extracted into Shared assembly so both Game.Core and Game.AI can reference it.
/// </summary>
public readonly struct ShipKinematics
{
    public readonly Vector2 pos;     // plane-space position
    public readonly Vector2 vel;     // plane-space velocity
    public readonly Vector2 forward; // plane-space forward (unit)
    public readonly float   angleDeg;// yaw in degrees

    public ShipKinematics(Vector2 pos, Vector2 vel, float angleDeg)
    {
        this.pos      = pos;
        this.vel      = vel;
        this.angleDeg = angleDeg;
        float a  = angleDeg * Mathf.Deg2Rad;
        forward  = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
    }
} 
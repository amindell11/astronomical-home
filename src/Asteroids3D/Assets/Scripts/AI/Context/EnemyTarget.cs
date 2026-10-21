using Movement;

namespace AI.Context
{
    /// <summary>
    /// A snapshot of the ship the AI is engaging. The one place ship-ness is captured, so
    /// downstream consumers — the navigator's MPC tactical costs and the gunner's firing
    /// solution — work off plain kinematics + dynamics without ever touching
    /// <see cref="Ships.Ship"/>.
    /// </summary>
    public struct EnemyTarget
    {
        public Kinematics kinematics;   // pos, vel, forward, yaw, yawRate
        public Dynamics dynamics;       // enemy motion model for the MPC rollout
    }
}

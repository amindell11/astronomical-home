using Movement;
using UnityEngine;

namespace AI.Context
{
    /// <summary>
    /// A snapshot of the ship the AI is engaging. The one place ship-ness is captured, so
    /// downstream consumers — the navigator's MPC tactical costs and the gunner's firing
    /// solution — work off plain kinematics + dynamics without ever touching
    /// <see cref="Ships.Ship"/>. Produced solely by <see cref="EnemyTracker.TryGetTarget"/>.
    /// </summary>
    public struct EnemyTarget
    {
        public Kinematics kinematics;   // pos, vel, forward, yaw, yawRate
        public Dynamics dynamics;       // enemy motion model for the MPC rollout
        public Transform source;        // identity: obstacle exclusion / debug
    }
}

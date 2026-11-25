using Movement;
using Ships.Command;
using Ships.Loadout;
using UnityEngine;

namespace Ships.Movement
{
    public struct Forces
    {
        private static Vector2 Thrust(Kinematics kin, float input, float forwardAccl, float revAccl)
        {
            var mag = input >= 0 ? forwardAccl : revAccl;
            var thrust = kin.Forward * (input * mag);
            return thrust;
        }

        private static Vector2 Strafe(Kinematics kin, float input, float maxStrafeForce, float minStrafeForce, float maxSpeed)
        {
            var speedPct = kin.vel.magnitude / maxSpeed;
            var mag = Mathf.Lerp(maxStrafeForce, minStrafeForce, speedPct);
            var right = new Vector2(kin.Forward.y, -kin.Forward.x);
            var strafeV = right * (input * mag);
            return strafeV;
        }

        private static float YawTorque(Kinematics kin, float input, float rotationThrust)
        {
            return input * rotationThrust;
        }
        
        private static float Bank(float input, float maxBankAngle)
        {
            return -input * maxBankAngle;
        }

        /// <param name="speedScale">A keystone's live multiplier on the cap, so strafe authority falls off against the cap the solver plans with.</param>
        public static Outputs ComputeOutputs(Kinematics kin, PilotCommand cmd, ResolvedShipStats sets, float speedScale = 1f)
        {
            if (sets == null)
            {
                return Outputs.Zero;
            }

            return new Outputs(
                Thrust(kin, cmd.thrust, sets.forwardForce, sets.reverseForce),
                Strafe(kin, cmd.strafe, sets.maxStrafeForce, sets.minStrafeForce, sets.maxSpeed * speedScale),
                YawTorque(kin, cmd.yawTorque, sets.yawTorque),
                Bank(cmd.strafe, sets.maxBankAngle)
            );
        }

    }
}

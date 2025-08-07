using UnityEngine;

namespace ShipMain.Movement
{
    internal class Mover
    {        
        private float nextBoostTime = 0f;
        
        public bool BoostAvailable => Time.time > nextBoostTime;

        internal (Vector2, Vector2, Vector2, float, float) CalculateInputs(Kinematics kin, Command cmd, Settings sets){
            var thrust = Calculator.Thrust(kin, cmd.Thrust, sets.forwardAccel, sets.reverseAccel);
            var strafe = Calculator.Strafe(kin, cmd.Strafe, sets.maxStrafeForce, sets.minStrafeForce, sets.maxSpeed);
            var boost = CalculateBoost(kin, cmd.Boost, sets.boostImpulse, sets.boostCooldown);
            var yawTorque = Calculator.YawTorque(kin, cmd.YawTorque, cmd.RotateToTarget, cmd.TargetAngle, sets.yawDeadZone, sets.rotationThrust);
            var bank = Calculator.Bank(kin, cmd.Strafe, sets.maxBankAngle, sets.bankingSpeed);

            return (thrust, strafe, boost, yawTorque, bank);
        }

        private Vector2 CalculateBoost(Kinematics kin, float boost, float boostStrength, float cooldown)
        {
            if (!BoostAvailable || boost == 0) return Vector2.zero;
            nextBoostTime = Time.time + cooldown;
            return Calculator.Boost(kin,  boost, boostStrength);
        }
    }
}
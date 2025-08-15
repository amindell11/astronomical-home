using ShipMain;
using UnityEngine;

namespace ShipMain.Movement
{
    public class Actuator
    {
        private Settings settings;
        private Command currentCommand;

        private readonly Booster booster = new Booster();

        public bool BoostAvailable => booster.BoostAvailable;
        public Outputs Outputs { get; private set; } = Movement.Outputs.Zero;

        public Command CurrentCommand => currentCommand;
        public Kinematics Kinematics { get; private set; }

        public Outputs GetOutputs(Kinematics kin){
            SetKinematics(kin);
            ComputeOutputs();
            return Outputs;
        }


        private void ComputeOutputs()
        {
            if (!settings)
            {
                Outputs = Movement.Outputs.Zero;
                return;
            }

            var thrust = Calculator.Thrust(Kinematics, currentCommand.Thrust, settings.forwardAccel, settings.reverseAccel);
            var strafe = Calculator.Strafe(Kinematics, currentCommand.Strafe, settings.maxStrafeForce, settings.minStrafeForce, settings.maxSpeed);
            var boost = booster.Boost(Kinematics, currentCommand.Boost, settings.boostImpulse, settings.boostCooldown);
            var yawTorque = Calculator.YawTorque(Kinematics, currentCommand.YawTorque, currentCommand.RotateToTarget, currentCommand.TargetAngle, settings.yawDeadZone, settings.rotationThrust);
            var bank = Calculator.Bank(Kinematics, currentCommand.Strafe, settings.maxBankAngle, settings.bankingSpeed);

            Outputs = new Outputs(thrust, strafe, boost, yawTorque, bank);
        }
        
        public void SetSettings(Settings newSettings)
        {
            settings = newSettings;
        }

        public void SetKinematics(in Kinematics kin)
        {
            Kinematics = kin;
        }

        public void SetCommand(in Command cmd)
        {
            currentCommand = cmd;
        }

        public void ResetOutputs()
        {
            Outputs = Movement.Outputs.Zero;
        }
    }
}
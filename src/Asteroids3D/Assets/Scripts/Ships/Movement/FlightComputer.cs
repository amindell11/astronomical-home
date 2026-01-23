namespace Ships.Movement
{
    public class FlightComputer
    {
        private readonly Booster booster = new Booster();
        private readonly Handling handling = new Handling();
        public bool BoostAvailable => booster.BoostAvailable;
        
        public Kinematics Kinematics { get; private set; }
        public Command CurrentCommand { get; private set; }
        private Settings Settings { get; set; }
        public Outputs Outputs { get; private set; }
        
        private Outputs Process(Kinematics kin, Command cmd, Settings sets)
        {
            Kinematics = kin;
            CurrentCommand = cmd;
            Settings = sets;
            
            var yawInput = cmd.RotateToTarget ? handling.RotationPD(cmd.TargetAngle, kin.Yaw, kin.YawRate, sets.maxYawRate, 2) : cmd.YawTorque; //TODO when we move the PD to player side we can dump this magic number
            var boostInput = booster.ProcessBoost(cmd.Boost, sets.boostCooldown);
            
            var thrust = Forces.Thrust(kin, cmd.Thrust, sets.forwardAccel, sets.reverseAccel);
            var strafe = Forces.Strafe(kin, cmd.Strafe, sets.maxStrafeForce, sets.minStrafeForce, sets.maxSpeed);
            var boost  = Forces.Boost(kin, boostInput, sets.boostImpulse);
            var yawTorque = Forces.YawTorque(kin,yawInput,sets.yawTorque);
            var bank      = Forces.Bank(kin, cmd.Strafe, sets.maxBankAngle, sets.bankingSpeed);

            return new Outputs(thrust, strafe, boost, yawTorque, bank);
        }

        public Outputs Process(Kinematics state)
        {
            SetKinematics(state);
            Outputs = Process(Kinematics, CurrentCommand, Settings);
            return Outputs;
        }
        
        public void PopulateSettings(Settings s)
        {
            Settings = s;
        }

        public void SetKinematics(Kinematics kin)
        {
            Kinematics = kin;
        }
        
        public void SetCommand(Command cmd)
        {
            CurrentCommand = cmd;
        }
    }
}
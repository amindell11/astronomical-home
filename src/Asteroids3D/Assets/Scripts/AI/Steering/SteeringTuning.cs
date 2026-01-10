namespace AI.Steering
{
    public readonly struct SteeringTuning
    {
        public readonly float ForwardAcc;
        public readonly float ReverseAcc;
        public readonly float StrafeAcc;
        public readonly float DeadZone;

        public SteeringTuning(float forwardAcc, float reverseAcc, float strafeAcc, float deadZone)
        {
            ForwardAcc  = forwardAcc;
            ReverseAcc  = reverseAcc;
            StrafeAcc   = strafeAcc;
            DeadZone    = deadZone;
        }

        public static readonly SteeringTuning Default = new SteeringTuning(
            forwardAcc: 8f,
            reverseAcc: 4f,
            strafeAcc:  6f,
            deadZone:   0.1f);
    }
} 
namespace ShipMain
{
    /// <summary>
    /// Per-frame control command issued by an <see cref="ICommandSource"/>.
    /// All values are in the canonical ship reference frame (XY plane).
    /// </summary>
    public struct Command
    {   
    
        /// <summary>Normalized forward (+) / reverse (–) thrust input in the range [-1, 1].</summary>
        public float Thrust;

        /// <summary>Normalized strafe input in the range [-1, 1]. Positive is ship-right.</summary>
        public float Strafe;

        /// <summary>Triggers a boost impulse when positive. Represents normalized boost magnitude [0, 1]. Default 0.</summary>
        public float Boost;

        /// <summary>When true the ship should yaw to <see cref="TargetAngle"/> this step.</summary>
        public bool  RotateToTarget;

        /// <summary>Target yaw angle in degrees (0-360, CCW from +Y) if <see cref="RotateToTarget"/> is true.</summary>
        public float TargetAngle;

        /// <summary>Desired yaw rate input in the range [-1, 1]. Overrides RotateToTarget if non-zero.</summary>
        public float YawTorque;

        /// <summary>When true the ship should attempt to fire its primary weapon this step.</summary>
        public bool  PrimaryFire;

        /// <summary>When true the ship should attempt to fire its secondary weapon this step.</summary>
        public bool  SecondaryFire;
    }
}

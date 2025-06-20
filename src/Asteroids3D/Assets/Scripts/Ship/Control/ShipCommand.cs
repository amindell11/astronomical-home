namespace ShipControl
{
    /// <summary>
    /// Per-frame control command issued by an <see cref="IShipCommandSource"/>.
    /// All values are in the canonical ship reference frame (XY plane).
    /// </summary>
    public struct ShipCommand
    {
        /// <summary>Normalized forward (+) / reverse (–) thrust input in the range [-1, 1].</summary>
        public float Thrust;

        /// <summary>Normalized strafe input in the range [-1, 1]. Positive is ship-right.</summary>
        public float Strafe;

        /// <summary>When true the ship should yaw to <see cref="TargetAngle"/> this step.</summary>
        public bool  RotateToTarget;

        /// <summary>Target yaw angle in degrees (0-360, CCW from +Y) if <see cref="RotateToTarget"/> is true.</summary>
        public float TargetAngle;

        /// <summary>When true the ship should attempt to fire its primary weapon this step.</summary>
        public bool  Fire;
    }
} 
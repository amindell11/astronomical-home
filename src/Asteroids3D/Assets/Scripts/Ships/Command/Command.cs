namespace Ships.Command
{
    /// <summary>
    /// Per-frame control command issued by an <see cref="ICommandSource"/>.
    /// All values are in the canonical ship reference frame (XY plane).
    /// </summary>
    public struct Command
    {   
    
        /// <summary>Normalized forward (+) / reverse (–) thrust input in the range [-1, 1].</summary>
        public float thrust;

        /// <summary>Normalized strafe input in the range [-1, 1]. Positive is ship-right.</summary>
        public float strafe;

        /// <summary>Triggers a boost impulse when positive. Represents normalized boost magnitude [0, 1]. Default 0.</summary>
        public float boost;

        /// <summary>Desired yaw rate input in the range [-1, 1].</summary>
        public float yawTorque;

        /// <summary>When true the ship should attempt to fire its primary weapon this step.</summary>
        public bool  primaryFire;

        /// <summary>When true the ship should attempt to fire its secondary weapon this step.</summary>
        public bool  secondaryFire;
    }
}

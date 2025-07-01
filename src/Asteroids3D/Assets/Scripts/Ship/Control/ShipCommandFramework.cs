using ShipControl;

namespace ShipControl
{
    /// <summary>
    /// A component that can supply high-level control commands for a <see cref="Ship"/>.
    /// Multiple sources can co-exist (e.g. AI, player, BT) – the Ship chooses which one to honour.
    /// </summary>
    public interface IShipCommandSource
    {
        /// <summary>
        /// Initializes the command source with a reference to the ship it controls.
        /// Called once by the ship during its Awake phase.
        /// </summary>
        /// <param name="ship">The ship this source will be controlling.</param>
        void InitializeCommander(Ship ship);

        /// <summary>
        /// Attempt to obtain a control command for this frame.
        /// </summary>
        /// <param name="state">Current state of the ship (kinematics, weapons)</param>
        /// <param name="cmd">Output command structure (undefined if method returns false)</param>
        /// <returns>True if this source wishes to drive the ship this frame.</returns>
        bool TryGetCommand(ShipState state, out ShipCommand cmd);

        /// <summary>
        /// Priority of this source.  Higher values override lower ones when multiple sources are active.
        /// </summary>
        int Priority { get; }
    }

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

        /// <summary>Desired yaw rate input in the range [-1, 1]. Overrides RotateToTarget if non-zero.</summary>
        public float YawRate;

        /// <summary>When true the ship should attempt to fire its primary weapon this step.</summary>
        public bool  PrimaryFire;

        /// <summary>When true the ship should attempt to fire its secondary weapon this step.</summary>
        public bool  SecondaryFire;
    }

    public struct ShipState
    {
        public ShipKinematics Kinematics;

        public bool IsLaserReady;
        public float LaserHeatPct;
        public MissileLauncher.LockState MissileState;
        public int MissileAmmo;
        public float HealthPct;
        public float ShieldPct;
    }
} 
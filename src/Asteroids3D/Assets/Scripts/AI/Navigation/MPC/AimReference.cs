using System.Runtime.InteropServices;

namespace AI.Navigation.MPC
{
    /// <summary>The heading a ship's primary weapon needs this step and that heading's rate, published by the Gunner. The aim servo turns onto it, and while the servo owns yaw the MPC's facing term follows it. Default is none: only the constructor makes a live one.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct AimReference
    {
        public readonly bool live;
        public readonly float heading;  // Radians, MPC convention (fwd = (-sin, cos))
        public readonly float rate;     // Radians per second

        public static AimReference None => default;

        public AimReference(float heading, float rate)
        {
            live = true;
            this.heading = heading;
            this.rate = rate;
        }
    }
}

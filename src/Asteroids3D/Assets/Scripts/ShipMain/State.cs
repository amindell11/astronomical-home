using Weapons;

namespace ShipMain
{
    public struct State
    {
            public Movement.Kinematics Kinematics;

            public bool IsLaserReady;
            public float LaserHeatPct;
            public MissileLauncher.LockState MissileState;
            public int MissileAmmo;
            public float HealthPct;
            public float ShieldPct;
    }
}
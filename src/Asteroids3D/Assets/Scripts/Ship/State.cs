namespace ShipControl
{
    public struct State
    {
            public Kinematics Kinematics;

            public bool IsLaserReady;
            public float LaserHeatPct;
            public MissileLauncher.LockState MissileState;
            public int MissileAmmo;
            public float HealthPct;
            public float ShieldPct;
    }
}
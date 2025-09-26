using Weapons;

namespace Ships
{
    public struct State
    {
            public Movement.Kinematics Kinematics;

            public bool IsPrimaryReady;
            public bool IsSecondaryReady;
            public float HealthPct;
            public float ShieldPct;
    }
}
namespace Ships.Command
{
    public struct State
    {
            public Movement.Kinematics kinematics;

            public bool isPrimaryReady;
            public bool isSecondaryReady;
            public float healthPct;
            public float shieldPct;
    }
}
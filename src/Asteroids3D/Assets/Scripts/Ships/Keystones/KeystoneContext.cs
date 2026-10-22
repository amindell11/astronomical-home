using Movement;

namespace Ships.Keystones
{
    /// <summary>What a kit's sight sees on one tick: its own ship and the enemy the commander is engaging.</summary>
    public readonly struct KeystoneContext
    {
        public readonly Kinematics Self;
        public readonly float SelfMass;
        public readonly Kinematics Target;

        public KeystoneContext(in Kinematics self, float selfMass, in Kinematics target)
        {
            Self = self;
            SelfMass = selfMass;
            Target = target;
        }
    }
}

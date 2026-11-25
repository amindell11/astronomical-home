using UnityEngine;

namespace Ships.Keystones
{
    /// <summary>What a kit does to its ship on one step: an impulse to apply, a jump to make, and an acceleration to push.</summary>
    public readonly struct KeystoneEffect
    {
        public readonly Vector2 Impulse;
        public readonly Vector2 Displacement;
        public readonly Vector2 Acceleration;

        public KeystoneEffect(Vector2 impulse, Vector2 displacement, Vector2 acceleration = default)
        {
            Impulse = impulse;
            Displacement = displacement;
            Acceleration = acceleration;
        }

        public static implicit operator KeystoneEffect(Vector2 impulse) => new(impulse, Vector2.zero);

        public static KeystoneEffect operator +(KeystoneEffect a, KeystoneEffect b) =>
            new(a.Impulse + b.Impulse, a.Displacement + b.Displacement, a.Acceleration + b.Acceleration);
    }
}

using UnityEngine;

namespace Ships.Keystones
{
    /// <summary>What a kit does to its ship on one step: an impulse to apply and a jump to make.</summary>
    public readonly struct KeystoneEffect
    {
        public readonly Vector2 Impulse;
        public readonly Vector2 Displacement;

        public KeystoneEffect(Vector2 impulse, Vector2 displacement)
        {
            Impulse = impulse;
            Displacement = displacement;
        }

        public static implicit operator KeystoneEffect(Vector2 impulse) => new(impulse, Vector2.zero);
    }
}

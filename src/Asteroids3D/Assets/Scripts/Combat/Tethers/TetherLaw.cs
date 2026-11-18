using UnityEngine;

namespace Combat.Tethers
{
    public static class TetherLaw
    {
        public static float Reel(float length, float reelRate, float minLength, float dt) =>
            length <= minLength ? length : Mathf.Max(minLength, length - reelRate * dt);

        public static float Tension(float distance, float length, float separationSpeed, float stiffness, float damping)
        {
            var stretch = distance - length;
            if (stretch <= 0f) return 0f;
            return Mathf.Max(0f, stiffness * stretch + damping * separationSpeed);
        }
    }
}

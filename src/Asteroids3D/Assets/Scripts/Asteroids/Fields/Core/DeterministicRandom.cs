using UnityEngine;

namespace Asteroids.Fields.Core
{
    /// <summary>
    /// Small self-contained PCG-style random stream. The deterministic field
    /// must never depend on UnityEngine.Random (global, order-sensitive) —
    /// every asteroid draws its attributes from its own stream seeded by its
    /// stable ID, so results are independent of load order and platform.
    /// </summary>
    public struct DeterministicRandom
    {
        private uint state;

        public DeterministicRandom(uint seed)
        {
            // Zero state would lock the low-entropy start; nudge it.
            state = seed == 0 ? 0x9E3779B9u : seed;
            // Burn one step so consecutive integer seeds decorrelate.
            NextUInt();
        }

        public uint NextUInt()
        {
            state = state * 747796405u + 2891336453u;
            var word = ((state >> (int)((state >> 28) + 4u)) ^ state) * 277803737u;
            return (word >> 22) ^ word;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        public float Range(float minInclusive, float maxInclusive) =>
            minInclusive + (maxInclusive - minInclusive) * NextFloat();

        /// <summary>Uniform int in [0, maxExclusive).</summary>
        public int RangeInt(int maxExclusive) =>
            maxExclusive <= 0 ? 0 : (int)(NextUInt() % (uint)maxExclusive);

        /// <summary>Uniform direction on the unit circle.</summary>
        public Vector2 Direction2()
        {
            var angle = NextFloat() * (2f * Mathf.PI);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        /// <summary>Uniform random rotation (Shoemake's subgroup algorithm).</summary>
        public Quaternion RotationUniform()
        {
            var u1 = NextFloat();
            var u2 = NextFloat() * (2f * Mathf.PI);
            var u3 = NextFloat() * (2f * Mathf.PI);
            var a = Mathf.Sqrt(1f - u1);
            var b = Mathf.Sqrt(u1);
            return new Quaternion(
                a * Mathf.Sin(u2),
                a * Mathf.Cos(u2),
                b * Mathf.Sin(u3),
                b * Mathf.Cos(u3));
        }

        /// <summary>Order-sensitive integer mix (murmur3-style finalizer chain) for keying streams.</summary>
        public static uint Hash(int a, int b, int c, int d = unchecked((int)0x5BD1E995))
        {
            var h = Mix((uint)a);
            h = Mix(h ^ (uint)b);
            h = Mix(h ^ (uint)c);
            h = Mix(h ^ (uint)d);
            return h;
        }

        private static uint Mix(uint h)
        {
            h ^= h >> 16;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return h;
        }
    }
}

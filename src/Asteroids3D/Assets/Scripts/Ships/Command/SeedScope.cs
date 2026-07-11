namespace Ships.Command
{
    /// <summary>
    /// An immutable, reproducible seed namespace for one agent. A per-agent root scope is split into
    /// independent child streams via <see cref="Derive"/>, so every RNG the agent drives (MPC sampler,
    /// strategy softmax, patrol) starts from its own stable sequence without sharing draw order. It
    /// holds no RNG state and no runtime identity, so a scope replays bit-for-bit across reconstructed
    /// episodes and processes — the substrate the RL/self-play phase is built on.
    ///
    /// Each composing owner names only its own child streams with small fixed constants; because
    /// siblings are derived from distinct parent scopes, identical ids under different parents never
    /// collide. A stream id must be a fixed constant — never an array index, name hash, or object
    /// identity, which would change replay under an unrelated reorder or rename.
    /// </summary>
    public readonly struct SeedScope
    {
        private readonly uint state;

        public SeedScope(int seed) => state = (uint)seed;
        private SeedScope(uint state) => this.state = state;

        /// <summary>A stable child scope for one named substream.</summary>
        public SeedScope Derive(uint streamId)
        {
            var h = (state ^ streamId) * 2654435761u;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            return new SeedScope(h);
        }

        /// <summary>Nonzero seed for a <see cref="System.Random"/>, kept positive to dodge the
        /// <c>int.MinValue</c> case that its constructor rejects.</summary>
        public int ToSeed() => (int)((state & 0x7FFFFFFFu) | 1u);

        /// <summary>Nonzero seed for a <c>Unity.Mathematics.Random</c>, which rejects a zero seed.</summary>
        public uint ToUint() => state == 0u ? 1u : state;
    }
}

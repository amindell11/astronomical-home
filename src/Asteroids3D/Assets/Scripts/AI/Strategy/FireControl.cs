namespace AI
{
    /// <summary>One slot's trigger authority for one decision: the Gunner's own firing solution (<see cref="Auto"/>), a held trigger the brain owns (<see cref="Commanded"/>), or nothing.</summary>
    public readonly struct FireControl
    {
        private enum Authority : byte { Hold, Gunner, Brain }

        private readonly Authority authority;
        private readonly bool held;

        private FireControl(Authority authority, bool held)
        {
            this.authority = authority;
            this.held = held;
        }

        /// <summary>Silence: the slot receives no command at all, so a charge weapon never sees the release it would fire on.</summary>
        public static FireControl Hold => default;

        public static FireControl Auto => new(Authority.Gunner, false);

        public static FireControl Commanded(bool held) => new(Authority.Brain, held);

        public bool IsAuto => authority == Authority.Gunner;

        public bool IsCommanded => authority == Authority.Brain;

        /// <summary>The commanded trigger state; the press edge is the commander's to derive, never the brain's.</summary>
        public bool Held => held;
    }
}

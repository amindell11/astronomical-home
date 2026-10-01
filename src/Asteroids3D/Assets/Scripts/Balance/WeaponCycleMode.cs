namespace Balance
{
    /// <summary>
    /// One way of firing a weapon, as <see cref="WeaponCycleProbe"/> measured it running the real
    /// weapon. The opening burst is what the weapon delivers from cold; the magazine, dump and
    /// recovery describe the burst it then repeats. "Magazine" is damage, not a round count (#772).
    /// </summary>
    public readonly struct WeaponCycleMode
    {
        public WeaponCycleMode(string label, float openingDamage, float openingSeconds,
            float magazineDamage, float dumpSeconds, float recoverySeconds)
        {
            Label = label;
            OpeningDamage = openingDamage;
            OpeningSeconds = openingSeconds;
            MagazineDamage = magazineDamage;
            DumpSeconds = dumpSeconds;
            RecoverySeconds = recoverySeconds;
        }

        public string Label { get; }
        public float OpeningDamage { get; }
        public float OpeningSeconds { get; }
        public float MagazineDamage { get; }
        public float DumpSeconds { get; }
        public float RecoverySeconds { get; }

        public float CycleSeconds => DumpSeconds + RecoverySeconds;
        public float SustainedDps => MagazineDamage / CycleSeconds;

        /// <summary>Share of one ship the opening burst removes.</summary>
        public float Stakes(float shipResourcePool) => OpeningDamage / shipResourcePool;
    }
}

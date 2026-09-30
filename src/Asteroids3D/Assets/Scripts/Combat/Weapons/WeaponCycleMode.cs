namespace Combat.Weapons
{
    /// <summary>
    /// One way of firing a weapon, as the numbers a balance pass compares. The magazine is
    /// damage delivered before the weapon is forced to stop (overheat, empty rounds, one
    /// charged shot) — not a round count. A weapon states its modes through
    /// <see cref="WeaponComponent.CycleModes"/>; everything derived from them lives here, so
    /// no reader dispatches on weapon type (#772).
    /// </summary>
    public readonly struct WeaponCycleMode
    {
        public WeaponCycleMode(string label, float magazineDamage, float dumpSeconds, float recoverySeconds)
        {
            Label = label;
            MagazineDamage = magazineDamage;
            DumpSeconds = dumpSeconds;
            RecoverySeconds = recoverySeconds;
        }

        public string Label { get; }
        public float MagazineDamage { get; }
        public float DumpSeconds { get; }
        public float RecoverySeconds { get; }

        public float CycleSeconds => DumpSeconds + RecoverySeconds;
        public float SustainedDps => MagazineDamage / CycleSeconds;

        /// <summary>Share of one ship this mode's magazine removes.</summary>
        public float Stakes(float shipResourcePool) => MagazineDamage / shipResourcePool;
    }
}

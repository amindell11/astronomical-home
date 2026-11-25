using System;

namespace Combat.Weapons.Mods
{
    /// <summary>
    /// The mods in force on one mounted weapon, summed per field, which its conditions read their
    /// effective values through. <see cref="None"/> is the authored weapon: a prefab asset, where no
    /// controller hands mods down, always reads through it, so enemies and the hangar see authored numbers.
    /// </summary>
    public sealed class WeaponMods
    {
        public static readonly WeaponMods None = new(new float[WeaponModFields.Count], 0);

        private readonly float[] bonus;
        private readonly int rounds;

        internal WeaponMods(float[] bonus, int rounds)
        {
            if (bonus == null || bonus.Length != WeaponModFields.Count)
                throw new ArgumentException("One bonus per weapon mod field.", nameof(bonus));
            this.bonus = bonus;
            this.rounds = rounds;
        }

        public float Factor(WeaponModField field) => 1f + bonus[(int)field];

        /// <summary>The effective value of an authored field: scaled up, or a rate field's seconds divided.</summary>
        public float Apply(WeaponModField field, float authored) =>
            field.IsRate() ? authored / Factor(field) : authored * Factor(field);

        public int Rounds(int authored) => authored + rounds;
    }
}

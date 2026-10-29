using System;

namespace Ships.Mods
{
    /// <summary>
    /// The ship mods in force on one ship, summed per field group, which the resolved stats apply once
    /// at resolve. <see cref="None"/> is the authored ship: a ship with no bound stack resolves through
    /// it, so enemies and the hangar read authored numbers.
    /// </summary>
    public sealed class ShipMods
    {
        public static readonly ShipMods None = new(new float[ShipModFields.Count]);

        private readonly float[] bonus;

        internal ShipMods(float[] bonus)
        {
            if (bonus == null || bonus.Length != ShipModFields.Count)
                throw new ArgumentException("One bonus per ship mod field.", nameof(bonus));
            this.bonus = bonus;
        }

        public float Factor(ShipModField field) => 1f + bonus[(int)field];

        /// <summary>The effective value of an authored field: scaled up, or a rate field's seconds divided.</summary>
        public float Apply(ShipModField field, float authored) =>
            field.IsRate() ? authored / Factor(field) : authored * Factor(field);
    }
}

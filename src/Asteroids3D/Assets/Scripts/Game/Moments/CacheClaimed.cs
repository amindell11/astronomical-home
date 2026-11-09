using System;
using System.Collections.Generic;
using System.Globalization;

namespace Game.Moments
{
    public sealed class CacheClaimed : IRunMoment
    {
        public const string CacheSlot = "cache-claimed";

        /// <summary>1-based.</summary>
        public int Sector { get; }
        /// <summary>The rolled bonus's fixed key: damage, fire-rate, shield or hull.</summary>
        public string Bonus { get; }
        public string Slot => CacheSlot;
        public IReadOnlyDictionary<string, string> Facts { get; }

        public CacheClaimed(int sector, string bonus)
        {
            if (string.IsNullOrEmpty(bonus)) throw new ArgumentException("A cache claim names its bonus.", nameof(bonus));
            Sector = sector;
            Bonus = bonus;
            Facts = new Dictionary<string, string>
            {
                ["sector"] = sector.ToString(CultureInfo.InvariantCulture),
                ["bonus"] = bonus
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using Damage;

namespace Game.Moments
{
    public sealed class PlayerDied : IRunMoment
    {
        public const string DeathSlot = "death";
        public const string CrashSlot = "death.crash";
        public const string SelfSlot = "death.self";

        public static readonly IReadOnlyList<string> AllSlots = new[] { DeathSlot, CrashSlot, SelfSlot };

        /// <summary>1-based.</summary>
        public int Sector { get; }
        public DamageKind KilledBy { get; }
        public DeathCause Cause { get; }
        public string Faction { get; }
        public string Slot { get; }
        public IReadOnlyDictionary<string, string> Facts { get; }

        public PlayerDied(int sector, DamageKind killedBy, DeathCause cause, string faction)
        {
            if (faction == null) throw new ArgumentNullException(nameof(faction));
            if (cause != DeathCause.Enemy && faction.Length != 0)
                throw new ArgumentException($"A {cause} death has no killer, so no faction.", nameof(faction));
            Sector = sector;
            KilledBy = killedBy;
            Cause = cause;
            Faction = faction;
            Slot = cause switch
            {
                DeathCause.Enemy => DeathSlot,
                DeathCause.Crash => CrashSlot,
                DeathCause.Self => SelfSlot,
                _ => throw new ArgumentOutOfRangeException(nameof(cause), cause, "Unknown death cause."),
            };
            Facts = new Dictionary<string, string>
            {
                ["sector"] = sector.ToString(CultureInfo.InvariantCulture),
                ["killed_by"] = killedBy.ToString(),
                ["faction"] = faction,
            };
        }
    }
}

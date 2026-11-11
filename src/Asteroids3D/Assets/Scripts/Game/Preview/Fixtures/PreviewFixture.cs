using System;
using Substrate.Sectors;
using Substrate.Sessions;
using UnityEngine;

namespace Game.Preview.Fixtures
{
    /// <summary>
    /// Which fields of a preview fixture keep the fixture's own value. Waves is the whole
    /// phases array. Locale includes whether the row is Random.
    /// </summary>
    [Flags]
    public enum PreviewFixtureOwn
    {
        None = 0,
        Faction = 1 << 0,
        Elite = 1 << 1,
        Waves = 1 << 2,
        Patrols = 1 << 3,
        Field = 1 << 4,
        Locale = 1 << 5,
        Armories = 1 << 6,
    }

    /// <summary>
    /// A preview layout and the game-run row it reads. <see cref="link"/> is <see cref="Unlinked"/>,
    /// or a 1-based sector number. <see cref="Resolve"/> returns a new row: that sector's row, with
    /// <see cref="layout"/> as its prefab, and each ticked field taken from <see cref="values"/>.
    /// Unlinked, every field comes from <see cref="values"/>.
    /// </summary>
    [Serializable]
    public sealed class PreviewFixture
    {
        public const int Unlinked = 0;

        public Sector layout;

        public int link = Unlinked;

        public PreviewFixtureOwn own;

        public SectorEntry values = new();

        public SectorEntry Resolve(SectorRun run)
        {
            if (!run) throw new ArgumentNullException(nameof(run));
            if (link == Unlinked) return Copy(values);
            var index = link - 1;
            if (index < 0 || index >= run.Sectors.Count)
                throw new InvalidOperationException(
                    $"The {LayoutName} fixture links to Sector {link}, outside the run's {run.Sectors.Count} sectors.");
            var resolved = Copy(run.Sectors[index]);
            if ((own & PreviewFixtureOwn.Faction) != 0) resolved.faction = values.faction;
            if ((own & PreviewFixtureOwn.Elite) != 0) resolved.elite = values.elite;
            if ((own & PreviewFixtureOwn.Waves) != 0) resolved.phases = values.phases;
            if ((own & PreviewFixtureOwn.Patrols) != 0) resolved.patrols = values.patrols;
            if ((own & PreviewFixtureOwn.Armories) != 0) resolved.armories = values.armories;
            if ((own & PreviewFixtureOwn.Field) != 0) resolved.field = values.field;
            if ((own & PreviewFixtureOwn.Locale) != 0)
            {
                resolved.locale = values.locale;
                resolved.random = values.random;
            }
            return resolved;
        }

        private string LayoutName => layout ? layout.name : "unnamed";

        private SectorEntry Copy(SectorEntry row)
        {
            var copy = row.WithLocale(row.locale, row.random);
            copy.prefab = layout;
            return copy;
        }
    }
}

using System;
using System.Collections;
using Substrate.Sectors.Elements;
using Substrate.Services.Locales;
using Substrate.Sessions;

namespace Substrate.Sectors.Factions
{
    public class SectorFaction : SectorModule, ISectorRowReader
    {
        /// <summary>The faction this sector's wave director, hunt and elite fly; the row's, from this module's setup on.</summary>
        public Faction Faction { get; private set; }

        /// <summary>The locale the row shows, which the faction dresses its ships for; none headless on a Random row.</summary>
        public LocaleSky Locale { get; private set; }

        public void CheckRow(SectorEntry row)
        {
            if (!row.faction)
                throw new InvalidOperationException($"SectorFaction on '{name}' reads the row's faction, and the row names none.");
        }

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            CheckRow(ctx.Row);
            Faction = ctx.Row.faction;
            Locale = ctx.Row.locale;
            yield break;
        }

#if UNITY_EDITOR
        internal void Bind(Faction faction) => Faction = faction;
#endif
    }
}

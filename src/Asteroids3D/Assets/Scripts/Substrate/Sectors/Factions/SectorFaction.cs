using System;
using System.Collections;
using Balance;
using Substrate.Sectors.Elements;
using UnityEngine;

namespace Substrate.Sectors.Factions
{
    public class SectorFaction : SectorModule
    {
        [Tooltip("The faction this sector belongs to: its wave director and hunt field that faction's ships.")]
        [Stat, SerializeField] private Faction faction;

        public Faction Faction => faction;

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!faction)
                throw new InvalidOperationException($"SectorFaction on '{name}' names no faction.");
            yield break;
        }

#if UNITY_EDITOR
        internal void Bind(Faction faction) => this.faction = faction;
#endif
    }
}

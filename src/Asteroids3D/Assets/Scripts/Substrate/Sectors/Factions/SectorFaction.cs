using System;
using System.Collections;
using Balance;
using Ships;
using Substrate.Sectors.Elements;
using UnityEngine;

namespace Substrate.Sectors.Factions
{
    public class SectorFaction : SectorModule
    {
        [Tooltip("The faction this sector belongs to: its wave director fields that faction's ships.")]
        [Stat, SerializeField] private Faction faction;

        private Ship hero;
        private int sectorIndex;
        private bool entered;

        /// <summary>The sector's index in the run and its faction's id, on the hero's first active frame here.</summary>
        public event Action<int, string> Entered;

        public Faction Faction => faction;

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!faction)
                throw new InvalidOperationException($"SectorFaction on '{name}' names no faction.");

            hero = ctx.Hero;
            sectorIndex = ctx.SectorIndex;
            entered = false;
            yield break;
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            hero = null;
            yield break;
        }

        // Raised here, not in Setup: the sector builds inside its load, before anyone can subscribe.
        private void Update()
        {
            if (entered || !hero || !hero.gameObject.activeInHierarchy) return;
            entered = true;
            Entered?.Invoke(sectorIndex, faction.id);
        }

#if UNITY_EDITOR
        internal void Bind(Faction faction) => this.faction = faction;
#endif
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using Balance;
using Loot;
using Ships;
using Substrate.Services.Objectives;
using UnityEngine;

namespace Substrate.Sectors.Elements.Phases
{
    /// <summary>
    /// The lull beacon's cache: each claim of the spine's beacon rolls one bonus from the feel's list, among
    /// those that fit the hero, adds it to the run's mods and shows it on the HUD with its seconds left. Each
    /// bonus runs out on its own clock, or at teardown, and is taken back off the run's mods. A sector whose
    /// beacons don't run (headless, no hero) never claims one, so never grants.
    /// The arc and its rulings: https://github.com/amindell11/astronomical-home/issues/1319
    /// </summary>
    public class BeaconCache : SectorModule
    {
        [Tooltip("Bonus duration and the list a claim rolls from.")]
        [Stat, SerializeField] private BeaconCacheFeelProfile feel;
        [Tooltip("The sector's spine, read for its lull beacon's claim.")]
        [SerializeField] private PhaseSpineModule spine;

        private readonly List<Grant> grants = new();
        private readonly List<LullMod> fitting = new();
        private Ship hero;
        private IObjectiveService objectives;
        private bool wasClaimed;

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!feel || !spine || ctx.Objectives == null || feel.bonuses.Length == 0
                || Array.Exists(feel.bonuses, bonus => !bonus))
                throw new InvalidOperationException(
                    $"BeaconCache on '{name}' needs its feel profile with a bonus list of mod assets, its spine and the objective service.");

            ClearGrants();
            hero = ctx.Hero;
            objectives = ctx.Objectives;
            wasClaimed = false;
            yield break;
        }

        public override IEnumerator Teardown(SectorBuildContext ctx)
        {
            ClearGrants();
            hero = null;
            objectives = null;
            yield break;
        }

        private void Update()
        {
            if (!hero) return;
            Tick(Time.time);
        }

        private void Tick(float now)
        {
            var claimed = spine.BeaconClaimed;
            if (claimed && !wasClaimed) Roll(now);
            wasClaimed = claimed;

            for (var i = grants.Count - 1; i >= 0; i--)
            {
                if (now < grants[i].Shown.EndsAt) continue;
                grants[i].TakeBack();
                grants.RemoveAt(i);
            }
        }

        private void Roll(float now)
        {
            var mods = hero.Mods ?? throw new InvalidOperationException(
                $"BeaconCache on '{name}' grants into the hero's run mods, and hero '{hero.name}' has none bound.");
            fitting.Clear();
            foreach (var bonus in feel.bonuses)
                if (bonus.Fits(hero)) fitting.Add(bonus);
            if (fitting.Count == 0)
                throw new InvalidOperationException(
                    $"BeaconCache on '{name}': no bonus in its list fits hero '{hero.name}'; the list needs one every ship fits.");

            var pick = fitting[UnityEngine.Random.Range(0, fitting.Count)];
            mods.Add(pick);
            grants.Add(new Grant(mods, pick,
                objectives.ShowBoost($"{pick.DisplayName} {pick.Effect}", now + feel.cacheSeconds)));
        }

        private void ClearGrants()
        {
            foreach (var grant in grants) grant.TakeBack();
            grants.Clear();
        }

        private readonly struct Grant
        {
            private readonly LullModStack mods;
            private readonly LullMod bonus;
            public readonly SectorBoost Shown;

            public Grant(LullModStack mods, LullMod bonus, SectorBoost shown)
            {
                this.mods = mods;
                this.bonus = bonus;
                Shown = shown;
            }

            public void TakeBack()
            {
                mods.Remove(bonus);
                Shown.Close();
            }
        }

#if UNITY_EDITOR
        internal void Configure(BeaconCacheFeelProfile feel, PhaseSpineModule spine)
        {
            this.feel = feel;
            this.spine = spine;
        }
#endif
    }
}

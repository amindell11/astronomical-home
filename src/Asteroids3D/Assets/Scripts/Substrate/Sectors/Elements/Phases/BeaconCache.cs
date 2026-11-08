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
    /// The lull beacon's cache: each claim of the spine's beacon rolls one of the feel's mod bonuses that fit
    /// the hero, or the hull patch. A mod bonus joins the run's mods and shows on the HUD with its seconds
    /// left; it runs out on its own clock, or at teardown, and is taken back off the run's mods. The hull
    /// patch restores a fraction of hull at once and shows briefly, touching no mods. A sector whose beacons
    /// don't run (headless, no hero) never claims one, so never grants.
    /// The arc and its rulings: https://github.com/amindell11/astronomical-home/issues/1319
    /// </summary>
    public class BeaconCache : SectorModule
    {
        private const float PatchNoticeSeconds = 3f;

        [Tooltip("Bonus duration, the mod bonuses and the hull patch a claim rolls from.")]
        [Stat, SerializeField] private BeaconCacheFeelProfile feel;
        [Tooltip("The sector's spine, read for its lull beacon's claim.")]
        [SerializeField] private PhaseSpineModule spine;

        private readonly List<Grant> grants = new();
        private readonly List<SectorBoost> notices = new();
        private readonly List<LullMod> fitting = new();
        private Ship hero;
        private IObjectiveService objectives;
        private bool wasClaimed;

        public override IEnumerator Setup(SectorBuildContext ctx)
        {
            if (!feel || !spine || ctx.Objectives == null || Array.Exists(feel.bonuses, bonus => !bonus))
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
            for (var i = notices.Count - 1; i >= 0; i--)
            {
                if (now < notices[i].EndsAt) continue;
                notices[i].Close();
                notices.RemoveAt(i);
            }
        }

        private void Roll(float now)
        {
            fitting.Clear();
            foreach (var bonus in feel.bonuses)
                if (bonus.Fits(hero)) fitting.Add(bonus);
            var index = Pick(fitting.Count, feel.HullWeight(hero.Damage.Health.Pct), UnityEngine.Random.value);
            if (index < 0) PatchHull(now);
            else GrantMod(fitting[index], now);
        }

        /// <summary>
        /// The rolled option for a uniform <paramref name="roll"/> in [0, 1]: a fitting bonus's index, each
        /// weighing 1, or -1 for the hull patch at <paramref name="hullWeight"/>.
        /// </summary>
        internal static int Pick(int fittingBonuses, float hullWeight, float roll)
        {
            var total = fittingBonuses + hullWeight;
            if (total <= 0f)
                throw new InvalidOperationException("A beacon cache roll needs a fitting bonus or a weighted hull patch.");
            var weighted = roll * total;
            if (hullWeight > 0f && weighted >= fittingBonuses) return -1;
            return Mathf.Min((int)weighted, fittingBonuses - 1);
        }

        private void GrantMod(LullMod bonus, float now)
        {
            var mods = hero.Mods ?? throw new InvalidOperationException(
                $"BeaconCache on '{name}' grants into the hero's run mods, and hero '{hero.name}' has none bound.");
            mods.Add(bonus);
            grants.Add(new Grant(mods, bonus,
                objectives.ShowBoost($"{bonus.DisplayName} {bonus.Effect}", now + feel.cacheSeconds)));
        }

        private void PatchHull(float now)
        {
            hero.Damage.Health.RestoreFraction(feel.hullRestore);
            notices.Add(objectives.ShowBoost($"Hull patch +{Mathf.RoundToInt(feel.hullRestore * 100f)}% hull",
                now + PatchNoticeSeconds));
        }

        private void ClearGrants()
        {
            foreach (var grant in grants) grant.TakeBack();
            grants.Clear();
            foreach (var notice in notices) notice.Close();
            notices.Clear();
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

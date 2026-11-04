#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using AI;
using AI.Context;
using Combat.Targeting;
using Combat.Weapons;
using Movement;
using NUnit.Framework;
using RL.Opponents.Beats;
using Ships.Command;
using Ships.Registry;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>The beat pick: a lone pilot attacks or flanks, a crowded one holds, the always-pressing elite attacks however crowded, a beat holds until its length runs out unless its own early end fires past the minimum, ties pick the earlier beat, and each seed draws its own desires within the personality's variation. Also the hit clock the break-off reads, the hold ring inside every weapon's fire distance, and the authored pilots.</summary>
    [Category("AI")]
    public class BeatBrainEditModeTests
    {
        private const string Pilots = "Assets/Prefabs/Pilots/";
        private const string Personalities = "Assets/Settings/AI/Pilots/";

        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
                if (item) Object.DestroyImmediate(item);
            created.Clear();
        }

        private Personality Fixed(float attack = 1f, float flank = 0.5f, float hold = 0.6f, float breakOff = 1.5f)
        {
            var personality = ScriptableObject.CreateInstance<Personality>();
            created.Add(personality);
            personality.attack = attack;
            personality.flank = flank;
            personality.hold = hold;
            personality.breakOff = breakOff;
            personality.variation = 0f;
            return personality;
        }

        private static BeatSelector Selector(Personality personality, int seed = 1) =>
            new(personality, new System.Random(seed));

        private static BeatSituation Fight(int attackers = 0, float aimedAt = 0f, float shield = 1f, bool hit = false,
            bool flankReached = false) =>
            new(attackers, aimedAt, shield, hit, flankReached);

        private static BeatSelector Picked(Personality personality, in BeatSituation situation)
        {
            var selector = Selector(personality);
            selector.Advance(0f, in situation, targetChanged: true);
            return selector;
        }

        [Test]
        public void Pick_AloneOnTheTarget_RunsAnAttack()
        {
            Assert.AreEqual(Beat.AttackRun, Picked(Fixed(), Fight()).Beat);
        }

        [Test]
        public void Pick_PastTolerance_Holds_EvenWithTheTargetLookingAtThePilot()
        {
            var personality = Fixed();
            Assert.AreEqual(Beat.AttackRun, Picked(personality, Fight(attackers: 1)).Beat,
                "one attacker sits inside a tolerance of one");
            Assert.AreEqual(Beat.Hold, Picked(personality, Fight(attackers: 2)).Beat);
            Assert.AreEqual(Beat.Hold, Picked(personality, Fight(attackers: 2, aimedAt: 1f)).Beat);
        }

        [Test]
        public void Pick_FlanksWhenTheTargetLooksAtThePilot()
        {
            var personality = Fixed(flank: 0.6f);
            Assert.AreEqual(Beat.AttackRun, Picked(personality, Fight()).Beat);
            Assert.AreEqual(Beat.Flank, Picked(personality, Fight(aimedAt: 1f)).Beat);
        }

        [Test]
        public void Pick_LowShieldUnderFire_BreaksOff()
        {
            Assert.AreEqual(Beat.BreakOff, Picked(Fixed(), Fight(shield: 0f, hit: true)).Beat);
            Assert.AreEqual(Beat.AttackRun, Picked(Fixed(), Fight(shield: 0.9f, hit: true)).Beat,
                "a shield above the break-off fraction never breaks off");
        }

        [Test]
        public void Pick_TheElitePersonality_AttacksHoweverCrowdedOrHurt()
        {
            var elite = AssetDatabase.LoadAssetAtPath<Personality>(Personalities + "ElitePersonality.asset");
            for (var seed = 0; seed < 20; seed++)
            {
                var selector = Selector(elite, seed);
                selector.Advance(0f, Fight(attackers: 5, aimedAt: 1f, shield: 0f, hit: true), true);
                Assert.AreEqual(Beat.AttackRun, selector.Beat, $"seed {seed}");
            }
        }

        [Test]
        public void Commitment_HoldsTheBeatThroughACrowd_UntilItsLengthRunsOut()
        {
            var personality = Fixed();
            personality.attackSeconds = new Vector2(4.9f, 4.9f);
            var selector = Picked(personality, Fight());
            var crowded = Fight(attackers: 3);
            for (var step = 0; step < 24; step++)
            {
                selector.Advance(0.2f, in crowded, targetChanged: false);
                Assert.AreEqual(Beat.AttackRun, selector.Beat, $"re-picked mid-beat at {(step + 1) * 0.2f:F1}s");
            }
            selector.Advance(0.2f, in crowded, targetChanged: false);
            Assert.AreEqual(Beat.Hold, selector.Beat);
            Assert.AreEqual(2, selector.Picks);
        }

        [Test]
        public void Commitment_ANewTarget_PicksAtOnce()
        {
            var selector = Picked(Fixed(), Fight());
            selector.Advance(0.2f, Fight(attackers: 3), targetChanged: true);
            Assert.AreEqual(Beat.Hold, selector.Beat);
        }

        [Test]
        public void Commitment_ABreakOff_EndsOnItsShieldOnlyPastTheMinimum()
        {
            var personality = Fixed();
            personality.breakOffSeconds = new Vector2(10f, 10f);
            var selector = Picked(personality, Fight(shield: 0f, hit: true));
            Assert.AreEqual(Beat.BreakOff, selector.Beat);

            selector.Advance(personality.minBeatSeconds * 0.5f, Fight(shield: 1f), targetChanged: false);
            Assert.AreEqual(Beat.BreakOff, selector.Beat, "a beat never ends early before the minimum");
            selector.Advance(personality.minBeatSeconds, Fight(shield: 0.5f), targetChanged: false);
            Assert.AreEqual(Beat.BreakOff, selector.Beat, "the shield is not back yet");
            selector.Advance(0.2f, Fight(shield: 1f), targetChanged: false);
            Assert.AreEqual(Beat.AttackRun, selector.Beat);
        }

        [Test]
        public void Tie_PicksTheEarlierBeatInScoringOrder()
        {
            Assert.AreEqual(Beat.AttackRun, Picked(Fixed(attack: 0.5f, flank: 0.5f, hold: 0.5f), Fight()).Beat);
            Assert.AreEqual(Beat.Flank, Picked(Fixed(attack: 0f, flank: 0.5f, hold: 0.5f), Fight()).Beat);
        }

        [Test]
        public void Variation_ASeedDrawsItsOwnDesires_WithinTheVariation()
        {
            var personality = Fixed();
            personality.variation = 0.25f;
            var draws = Enumerable.Range(0, 10).Select(seed => Selector(personality, seed)).ToArray();

            Assert.AreEqual(draws[3].Attack, Selector(personality, 3).Attack, "a seed always draws the same pilot");
            Assert.Greater(draws.Select(draw => draw.Attack).Distinct().Count(), 5, "seeds draw different pilots");
            foreach (var draw in draws)
            {
                Assert.That(draw.Attack, Is.InRange(0.75f, 1.25f));
                Assert.That(draw.Crowding, Is.InRange(0.6f, 1f));
            }
        }

        [Test]
        public void SecondsSinceHit_ZeroesOnAShieldOrHullLoss_AndCountsUpOtherwise()
        {
            var host = new GameObject("HitClock");
            created.Add(host);
            var status = new StubStatus();
            var context = new AIContext(status, host.AddComponent<Scout>(), holdRange: null);
            Assert.IsTrue(float.IsPositiveInfinity(context.SecondsSinceHit));

            status.ShieldPct = 0.5f;
            context.Update(0.1f);
            Assert.AreEqual(0f, context.SecondsSinceHit);
            status.ShieldPct = 0.6f;
            context.Update(0.1f);
            context.Update(0.1f);
            Assert.AreEqual(0.2f, context.SecondsSinceHit, 1e-5f, "regeneration is not a hit");
            status.HealthPct = 0.9f;
            context.Update(0.1f);
            Assert.AreEqual(0f, context.SecondsSinceHit);
        }

        [Test]
        public void HoldRing_SitsInsideFireDistance_ForEveryShippedWeaponAndPilot()
        {
            var weapons = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Weapons" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)))
                .Select(prefab => prefab.GetComponent<WeaponComponent>())
                .Where(weapon => weapon && weapon.FireRange > 0f).ToArray();
            Assert.That(weapons.Select(weapon => weapon.name),
                Is.SupersetOf(new[] { "Lasers", "TurboLasers", "Rippers", "Railgun" }));
            var railgun = weapons.Single(weapon => weapon.name == "Railgun");
            var enemy = AssetDatabase.LoadAssetAtPath<Personality>(Personalities + "EnemyPersonality.asset");
            Assert.Greater(enemy.holdRangeScale * railgun.HoldRange.Value, railgun.FireRange,
                "test premise: the personality's own ring would leave a railgun out of range");

            using var pilot = new SerializedObject(
                AssetDatabase.LoadAssetAtPath<GameObject>(Pilots + "EnemyPilot.prefab").GetComponent<BeatBrain>());
            var attackRange = pilot.FindProperty("attackRange").floatValue;
            foreach (var personality in new[] { "Enemy", "Elite", "Escort" }.Select(name =>
                         AssetDatabase.LoadAssetAtPath<Personality>(Personalities + name + "Personality.asset")))
            foreach (var weapon in weapons)
            {
                var ring = BeatBrain.HoldRing(weapon.HoldRange ?? attackRange, weapon.FireRange, personality.holdRangeScale);
                Assert.Less(ring, weapon.FireRange, $"{personality.name} holding with {weapon.name}");
            }
        }

        [TestCase("EnemyPilot", "EnemyPersonality")]
        [TestCase("ElitePilot", "ElitePersonality")]
        [TestCase("EscortPilot", "EscortPersonality")]
        public void EachPilot_FliesTheBeatBrain_OnItsPersonality(string pilot, string personality)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Pilots + pilot + ".prefab");
            Assert.IsTrue(prefab && prefab.GetComponent<AICommander>(), $"{pilot} is a pilot");
            var brain = prefab.GetComponent<BeatBrain>();
            Assert.IsTrue(brain, $"{pilot} flies the beat brain");
            using var serialized = new SerializedObject(brain);
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Personality>(Personalities + personality + ".asset"),
                serialized.FindProperty("personality").objectReferenceValue);
        }

        private sealed class StubStatus : IShipStatus
        {
            public float HealthPct { get; set; } = 1f;
            public float ShieldPct { get; set; } = 1f;
            public ShipId Id => default;
            public Transform Transform => null;
            public Kinematics Kinematics => default;
            public Dynamics Dynamics => Dynamics.Default;
            public bool BoostAvailable => true;
            public float BoostCooldownRemaining => 0f;
            public float BoostCooldownPct => 0f;
            public float MaxSpeed => 10f;
            public float MaxYawRate => 90f;
            public IReadOnlyList<ITrackingMissile> TrackingMissiles => System.Array.Empty<ITrackingMissile>();
        }
    }
}
#endif

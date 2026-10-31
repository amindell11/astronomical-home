using System;
using System.Linq;
using Game;
using Game.Preview;
using NUnit.Framework;
using UI.Projection;
using UnityEditor;

namespace Tests.EditMode.Preview
{
    [Category("Bootstrap")]
    public class TuningTargetEditModeTests
    {
        private GameHost game;

        [SetUp]
        public void SetUp() => game = AssetDatabase.LoadAssetAtPath<GameHost>(PreviewHostBootstrap.GameHostPath);

        [Test]
        public void EachTarget_OffersExactlyItsEntries_AndTunesExactlyItsProfiles()
        {
            var canopy = AssetDatabase.LoadAssetAtPath<CanopyProjectionProfile>("Assets/Settings/UI/CanopyProjection.asset");
            AssertTarget(Target<SectorJumpTarget>(), new[] { PreviewEntry.SectorJump, PreviewEntry.CrowdedJumpGate },
                game.jumpFeel);
            AssertTarget(Target<RunRecapTarget>(), new[] { PreviewEntry.RunRecap, PreviewEntry.Combat }, game.recapFeel);
            AssertTarget(Target<HudTarget>(), new[] { PreviewEntry.Combat }, canopy);
        }

        [Test]
        public void SwitchingTargets_KeepsAnOfferedEntry_ElseTakesTheTargetsFirst()
        {
            Assert.AreEqual(PreviewEntry.Combat, Target<RunRecapTarget>().EntryAfterSwitch(PreviewEntry.Combat));
            Assert.AreEqual(PreviewEntry.Combat, Target<HudTarget>().EntryAfterSwitch(PreviewEntry.Combat));
            Assert.AreEqual(PreviewEntry.RunRecap, Target<RunRecapTarget>().EntryAfterSwitch(PreviewEntry.CrowdedJumpGate));
            Assert.AreEqual(PreviewEntry.Combat, Target<HudTarget>().EntryAfterSwitch(PreviewEntry.RunRecap));
            Assert.AreEqual(PreviewEntry.SectorJump, Target<SectorJumpTarget>().EntryAfterSwitch(PreviewEntry.Combat));
        }

        [Test]
        public void EveryPreviewEntry_IsOfferedBySomeTarget()
        {
            foreach (PreviewEntry entry in Enum.GetValues(typeof(PreviewEntry)))
                Assert.IsTrue(TuningTarget.All.Any(target => target.Offers(entry)), $"{entry} has no tuning target");
        }

        private static TuningTarget Target<T>() where T : TuningTarget => TuningTarget.All.OfType<T>().Single();

        private void AssertTarget(TuningTarget target, PreviewEntry[] entries, UnityEngine.Object profile)
        {
            Assert.IsTrue(profile, $"{target.Name}'s profile ships");
            Assert.That(target.Entries, Is.EqualTo(entries), target.Name);
            Assert.That(target.Profiles(game), Is.EqualTo(new[] { profile }), target.Name);
        }
    }
}

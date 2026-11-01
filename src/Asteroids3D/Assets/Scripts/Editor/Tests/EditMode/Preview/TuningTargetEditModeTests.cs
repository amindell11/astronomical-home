using System;
using System.Linq;
using Asteroids.Fields;
using Cameras;
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
            AssertTarget(Target<HudTarget>(), new[]
            {
                PreviewEntry.Startup, PreviewEntry.LowHull, PreviewEntry.Damage, PreviewEntry.Extraction, PreviewEntry.Combat
            }, canopy);
            AssertTarget(Target<FlightCameraTarget>(), new[] { PreviewEntry.Combat },
                AssetDatabase.LoadAssetAtPath<FlightCameraProfile>("Assets/Settings/Cameras/FlightCamera.asset"));
            var fields = new[] { "Sector1", "Sector2", "Big" }.Select(field =>
                AssetDatabase.LoadAssetAtPath<AsteroidFieldSettings>($"Assets/Settings/Asteroids/{field}FieldSettings.asset"));
            AssertTarget(Target<AsteroidsTarget>(), new[] { PreviewEntry.Combat }, fields.ToArray());
        }

        [Test]
        public void SwitchingTargets_KeepsAnOfferedEntry_ElseTakesTheTargetsFirst()
        {
            Assert.AreEqual(PreviewEntry.Combat, Target<RunRecapTarget>().EntryAfterSwitch(PreviewEntry.Combat));
            Assert.AreEqual(PreviewEntry.Combat, Target<HudTarget>().EntryAfterSwitch(PreviewEntry.Combat));
            Assert.AreEqual(PreviewEntry.RunRecap, Target<RunRecapTarget>().EntryAfterSwitch(PreviewEntry.CrowdedJumpGate));
            Assert.AreEqual(PreviewEntry.Startup, Target<HudTarget>().EntryAfterSwitch(PreviewEntry.RunRecap));
            Assert.AreEqual(PreviewEntry.SectorJump, Target<SectorJumpTarget>().EntryAfterSwitch(PreviewEntry.Combat));
        }

        [Test]
        public void EveryPreviewEntry_IsOfferedBySomeTarget()
        {
            foreach (PreviewEntry entry in Enum.GetValues(typeof(PreviewEntry)))
                Assert.IsTrue(TuningTarget.All.Any(target => target.Offers(entry)), $"{entry} has no tuning target");
        }

        private static TuningTarget Target<T>() where T : TuningTarget => TuningTarget.All.OfType<T>().Single();

        private void AssertTarget(TuningTarget target, PreviewEntry[] entries, params UnityEngine.Object[] profiles)
        {
            foreach (var profile in profiles) Assert.IsTrue(profile, $"{target.Name}'s profile ships");
            Assert.That(target.Entries, Is.EqualTo(entries), target.Name);
            Assert.That(target.Profiles(game), Is.EqualTo(profiles), target.Name);
        }
    }
}

using System;
using System.Linq;
using AI.Tactics;
using Asteroids.Fields;
using Cameras;
using Game;
using Game.Companion;
using Game.Player;
using Game.Preview;
using NUnit.Framework;
using Ships.Visuals.Breakup;
using Substrate.Sectors.Elements.Elites;
using Substrate.Sectors.Elements.Hunt;
using Substrate.Sectors.Elements.Stations;
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
            AssertTarget(Target<ShipDeathTarget>(), new[] { PreviewEntry.EnemyDeath, PreviewEntry.RunRecap, PreviewEntry.Combat },
                AssetDatabase.LoadAssetAtPath<DeathFeelProfile>("Assets/Settings/Ships/DeathFeel.asset"), game.recapFeel);
            AssertTarget(Target<FlightCameraTarget>(), new[] { PreviewEntry.Combat },
                AssetDatabase.LoadAssetAtPath<FlightCameraProfile>("Assets/Settings/Cameras/FlightCamera.asset"));
            var fields = new[] { "Sector1", "Sector2", "Big" }.Select(field =>
                AssetDatabase.LoadAssetAtPath<AsteroidFieldSettings>($"Assets/Settings/Asteroids/{field}FieldSettings.asset"));
            AssertTarget(Target<AsteroidsTarget>(), new[] { PreviewEntry.Combat }, fields.ToArray());
            AssertTarget(Target<FlightAssistTarget>(), new[] { PreviewEntry.Combat },
                AssetDatabase.LoadAssetAtPath<FlightAssistProfile>("Assets/Settings/Game/FlightAssist.asset"));
            AssertTarget(Target<HuntTarget>(), new[] { PreviewEntry.Combat },
                AssetDatabase.LoadAssetAtPath<HuntFeelProfile>("Assets/Settings/Game/HuntFeel.asset"),
                AssetDatabase.LoadAssetAtPath<RepairStationFeelProfile>("Assets/Settings/Game/RepairStationFeel.asset"));
            AssertTarget(Target("Sector 1 elite"), new[] { PreviewEntry.Sector1Elite },
                AssetDatabase.LoadAssetAtPath<EliteProfile>("Assets/Settings/Ships/Elites/ShoalElite.asset"));
            AssertTarget(Target("Sector 2 elite"), new[] { PreviewEntry.Sector2Elite },
                AssetDatabase.LoadAssetAtPath<EliteProfile>("Assets/Settings/Ships/Elites/VeilElite.asset"));
            AssertTarget(Target("Boss"), new[] { PreviewEntry.Boss },
                AssetDatabase.LoadAssetAtPath<EliteProfile>("Assets/Settings/Ships/Elites/BastionBoss.asset"));
            AssertTarget(Target<CompanionTarget>(), new[] { PreviewEntry.Combat },
                AssetDatabase.LoadAssetAtPath<CompanionFeelProfile>("Assets/Settings/Game/Companion/CompanionFeel.asset"),
                AssetDatabase.LoadAssetAtPath<CompanionLineSheet>("Assets/Settings/Game/Companion/CompanionLines.asset"));
            AssertTarget(Target<TacticsTarget>(), new[] { PreviewEntry.CrowdedFight },
                AssetDatabase.LoadAssetAtPath<TacticsSettings>("Assets/Settings/AI/Tactics/TacticsSettings.asset"));
        }

        [Test]
        public void SwitchingTargets_KeepsAnOfferedEntry_ElseTakesTheTargetsFirst()
        {
            Assert.AreEqual(PreviewEntry.Combat, Target<RunRecapTarget>().EntryAfterSwitch(PreviewEntry.Combat));
            Assert.AreEqual(PreviewEntry.Combat, Target<HudTarget>().EntryAfterSwitch(PreviewEntry.Combat));
            Assert.AreEqual(PreviewEntry.RunRecap, Target<RunRecapTarget>().EntryAfterSwitch(PreviewEntry.CrowdedJumpGate));
            Assert.AreEqual(PreviewEntry.Startup, Target<HudTarget>().EntryAfterSwitch(PreviewEntry.RunRecap));
            Assert.AreEqual(PreviewEntry.SectorJump, Target<SectorJumpTarget>().EntryAfterSwitch(PreviewEntry.Combat));
            Assert.AreEqual(PreviewEntry.RunRecap, Target<ShipDeathTarget>().EntryAfterSwitch(PreviewEntry.RunRecap));
            Assert.AreEqual(PreviewEntry.EnemyDeath, Target<ShipDeathTarget>().EntryAfterSwitch(PreviewEntry.CrowdedJumpGate));
        }

        [Test]
        public void EveryPreviewEntry_IsOfferedBySomeTarget()
        {
            foreach (PreviewEntry entry in Enum.GetValues(typeof(PreviewEntry)))
                Assert.IsTrue(TuningTarget.All.Any(target => target.Offers(entry)), $"{entry} has no tuning target");
        }

        private static TuningTarget Target<T>() where T : TuningTarget => TuningTarget.All.OfType<T>().Single();

        private static TuningTarget Target(string name) => TuningTarget.All.Single(target => target.Name == name);

        private void AssertTarget(TuningTarget target, PreviewEntry[] entries, params UnityEngine.Object[] profiles)
        {
            foreach (var profile in profiles) Assert.IsTrue(profile, $"{target.Name}'s profile ships");
            Assert.That(target.Entries, Is.EqualTo(entries), target.Name);
            Assert.That(target.Profiles(game), Is.EqualTo(profiles), target.Name);
        }
    }
}

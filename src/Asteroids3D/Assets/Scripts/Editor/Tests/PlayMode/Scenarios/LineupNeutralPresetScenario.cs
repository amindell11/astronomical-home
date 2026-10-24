#if UNITY_EDITOR
using System.Collections;
using System.IO;
using Capture;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEngine;

namespace Tests.PlayMode.Scenarios
{
    public sealed class LineupNeutralPresetScenario : CaptureScenario
    {
        public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;

        public override CaptureConfig Config => new()
        {
            clipName = "LineupNeutral",
            preset = CapturePreset.LineupNeutral,
        };

        public override IEnumerator Run()
        {
            var (a, _) = SpawnCombatShip(new Vector2(-8f, 0f), rotDeg: 40f, team: 0);
            var (b, _) = SpawnCombatShip(new Vector2(8f, 0f), rotDeg: -40f, team: 1);
            Film(a, b);
            Assert.That(Quaternion.Angle(a.transform.rotation, Quaternion.identity), Is.LessThan(0.5f));
            for (var i = 0; i < 15 && a && b; i++)
            {
                yield return new WaitForFixedUpdate();
                FilmStep();
                Assert.That(Quaternion.Angle(a.transform.rotation, Quaternion.identity), Is.LessThan(0.5f));
            }
            Assert.IsTrue(File.Exists(Path.Combine(Capture.FrameDir, "provenance.json")));
            Assert.IsTrue(File.Exists(Path.Combine(Capture.FrameDir, "phases.csv")));
            StringAssert.Contains(CapturePreset.LineupNeutral, File.ReadAllText(Path.Combine(Capture.FrameDir, "provenance.json")));
        }
    }
}
#endif

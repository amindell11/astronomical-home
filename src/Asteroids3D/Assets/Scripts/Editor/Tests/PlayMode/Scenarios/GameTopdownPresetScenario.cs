#if UNITY_EDITOR
using System.Collections;
using System.IO;
using Capture;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UnityEngine;

namespace Tests.PlayMode.Scenarios
{
    public sealed class GameTopdownPresetScenario : CaptureScenario
    {
        public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;

        public override CaptureConfig Config => new()
        {
            clipName = "GameTopdown",
            preset = CapturePreset.GameTopdown,
        };

        public override IEnumerator Run()
        {
            var (a, _) = SpawnCombatShip(new Vector2(-6f, 0f), rotDeg: -90f, team: 0);
            var (b, _) = SpawnCombatShip(new Vector2(6f, 0f), rotDeg: 90f, team: 1);
            Film(a, b);
            for (var i = 0; i < 25 && a && b; i++)
            {
                yield return new WaitForFixedUpdate();
                FilmStep();
            }
            AssertProvenance(CapturePreset.GameTopdown);
        }

        private void AssertProvenance(string source)
        {
            var dir = Capture.FrameDir;
            Assert.IsTrue(File.Exists(Path.Combine(dir, "provenance.json")), dir);
            Assert.IsTrue(File.Exists(Path.Combine(dir, "phases.csv")), dir);
            var provenance = File.ReadAllText(Path.Combine(dir, "provenance.json"));
            StringAssert.Contains(source, provenance);
            StringAssert.Contains("unscaled", provenance);
        }
    }
}
#endif

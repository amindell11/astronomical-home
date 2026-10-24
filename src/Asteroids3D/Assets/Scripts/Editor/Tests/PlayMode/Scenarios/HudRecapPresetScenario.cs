#if UNITY_EDITOR
using System.Collections;
using System.Globalization;
using System.IO;
using Capture;
using Game.Runs;
using NUnit.Framework;
using Tests.PlayMode.Common;
using UI.Recap;
using UI.Screens;
using UnityEditor;
using UnityEngine;

namespace Tests.PlayMode.Scenarios
{
    /// <summary>
    /// The game's outcome banner fades on unscaled time while the sim clock is paused.
    /// </summary>
    public sealed class HudRecapPresetScenario : CaptureScenario
    {
        private const string FeelPath = "Assets/Settings/UI/RecapFeel.asset";

        public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;

        public override CaptureConfig Config => new()
        {
            clipName = "HudRecap",
            preset = CapturePreset.Hud1280x720,
            everyFixedSteps = 1,
        };

        public override IEnumerator Run()
        {
            var (ship, _) = SpawnCombatShip(Vector2.zero, rotDeg: 0f, team: 0);
            Film(ship);
            for (var i = 0; i < 4; i++)
            {
                yield return null;
                FilmStep();
            }
            yield return null;

            var feel = AssetDatabase.LoadAssetAtPath<RecapFeelProfile>(FeelPath);
            Assert.IsNotNull(feel, FeelPath);
            var context = RecapOutcomeContext.Defeat(System.Array.Empty<string>(), 1, 3,
                new Scoreboard(System.Array.Empty<Scoreboard.Row>(), null), Vector2.zero);
            var sequence = new RecapOutcomeSequence(feel, context);
            var bannerSpan = -1;
            for (var i = 0; i < sequence.SpanCount; i++)
                if (sequence.Span(i).Phase == RecapPhase.BannerIn) bannerSpan = i;
            Assert.That(bannerSpan, Is.GreaterThanOrEqualTo(0));

            var screen = RunRecapScreen.Create(null, context);
            try
            {
                Time.timeScale = 0f;
                var origin = Time.unscaledTime;
                var elapsed = 0f;
                ApplyBanner(screen, sequence, bannerSpan, elapsed);
                var guard = 0;
                while (elapsed < feel.BannerInSeconds && guard++ < 80)
                {
                    yield return null;
                    elapsed += Time.unscaledDeltaTime;
                    ApplyBanner(screen, sequence, bannerSpan, elapsed);
                }
                Assert.That(sequence.StateAt(bannerSpan, elapsed).BannerAlpha, Is.GreaterThan(0.99f),
                    $"banner alpha after {Time.unscaledTime - origin} unscaled seconds");
                AssertFadeSpan(origin);
            }
            finally
            {
                if (screen) Object.Destroy(screen.gameObject);
            }
        }

        private void ApplyBanner(RunRecapScreen screen, RecapOutcomeSequence sequence, int span, float elapsed)
        {
            var state = sequence.StateAt(span, elapsed);
            screen.Apply(state);
            Capture.RecordPhase("banner_alpha", state.BannerAlpha);
            FilmStep();
        }

        private void AssertFadeSpan(float origin)
        {
            var path = Path.Combine(Capture.FrameDir, "phases.csv");
            var lines = File.ReadAllLines(path);
            Assert.That(lines.Length, Is.GreaterThan(2), path);
            var header = lines[0].Split(',');
            var alphaColumn = System.Array.IndexOf(header, "banner_alpha");
            var timeColumn = System.Array.IndexOf(header, "unscaled_seconds");
            Assert.That(alphaColumn, Is.GreaterThanOrEqualTo(0));
            float? low = null;
            float? high = null;
            for (var i = 1; i < lines.Length; i++)
            {
                if (lines[i].Length == 0) continue;
                var cells = lines[i].Split(',');
                if (alphaColumn >= cells.Length || cells[alphaColumn].Length == 0) continue;
                var alpha = float.Parse(cells[alphaColumn], CultureInfo.InvariantCulture);
                var time = float.Parse(cells[timeColumn], CultureInfo.InvariantCulture);
                if (low == null && alpha < 0.2f) low = time;
                if (low != null && high == null && alpha > 0.99f) high = time;
            }
            Assert.IsNotNull(low, "no recorded frame caught the banner near transparent");
            Assert.IsNotNull(high, "no recorded frame caught the banner fully shown");
            var span = high.Value - low.Value;
            Assert.That(span, Is.InRange(0.65f, 1.25f),
                $"banner fade spanned {span} unscaled seconds, authored duration is 0.8");
            Assert.That(high.Value, Is.GreaterThanOrEqualTo(origin));
            var provenance = File.ReadAllText(Path.Combine(Capture.FrameDir, "provenance.json"));
            StringAssert.Contains(CapturePreset.Hud1280x720, provenance);
            StringAssert.Contains("1280x720", provenance);
            StringAssert.Contains("unscaled", provenance);
        }
    }
}
#endif

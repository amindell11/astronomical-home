using System.IO;
using Diagnostics.Performance;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class LatencyProfilingEditModeTests
    {
        [Test]
        public void LatencyProfilingSettings_ParseScenarioArguments()
        {
            var settings = LatencyProfilingSettings.TryParse(new[]
            {
                "Asteroids3D.exe",
                "-latencyProfileScenario", "combat_baseline",
                "-latencyWarmupSeconds", "7",
                "-latencySampleSeconds", "45",
                "-latencyOutDir", "results/latency-profiling/test-run",
                "-latencySeed", "9001",
                "-latencyTeamBCount", "8",
                "-latencyAsteroidCount", "24",
                "-latencySpawnRadius", "75"
            });

            Assert.IsNotNull(settings);
            Assert.AreEqual("combat_baseline", settings.Scenario);
            Assert.AreEqual(7, settings.WarmupSeconds);
            Assert.AreEqual(45, settings.SampleSeconds);
            Assert.AreEqual(9001, settings.Seed);
            Assert.AreEqual(8, settings.TeamBCount);
            Assert.AreEqual(24, settings.AsteroidCount);
            Assert.AreEqual(75f, settings.SpawnRadius);
        }

        [Test]
        public void LatencyProfilingSettings_MissingScenarioDisablesProfiling()
        {
            var settings = LatencyProfilingSettings.TryParse(new[] { "Asteroids3D.exe", "-screen-width", "1280" });
            Assert.IsNull(settings);
        }

        [Test]
        public void LatencyProfilingSettings_ResolvesRelativeOutDir()
        {
            var settings = new LatencyProfilingSettings
            {
                OutDir = Path.Combine("results", "latency-profiling", "sample")
            };

            var resolved = settings.GetResolvedOutDir();
            StringAssert.EndsWith(Path.Combine("results", "latency-profiling", "sample"), resolved);
            Assert.IsTrue(Path.IsPathRooted(resolved));
        }
    }
}

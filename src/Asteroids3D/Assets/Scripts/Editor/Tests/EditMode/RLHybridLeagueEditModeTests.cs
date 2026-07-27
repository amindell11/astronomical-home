#if UNITY_EDITOR
using System;
using Game.RLHarness;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>Pins the per-worker hybrid league split (RL_HYBRID_SCRIPTED_WORKERS): under self-play the first k workers keep the scripted roster so pursuit retains a gradient, everyone else runs the mirror league, and outside self-play the knob is inert. The env-var parse is strict — a silently dropped k trains a pure mirror league that erodes exactly the capability the hybrid run protects.</summary>
    [Category("AI")]
    public class RLHybridLeagueEditModeTests
    {
        [Test]
        public void SelfPlay_WorkersBelowK_KeepTheScriptedRoster()
        {
            Assert.IsTrue(TrainingHost.SelectsScriptedRoster(selfPlayRun: true, workerIndex: 0, hybridScriptedWorkers: 2),
                "worker 0 (also the editor/manual null-index worker) is scripted under k=2");
            Assert.IsTrue(TrainingHost.SelectsScriptedRoster(selfPlayRun: true, workerIndex: 1, hybridScriptedWorkers: 2));
            Assert.IsFalse(TrainingHost.SelectsScriptedRoster(selfPlayRun: true, workerIndex: 2, hybridScriptedWorkers: 2),
                "worker k is the first mirror-league worker");
            Assert.IsFalse(TrainingHost.SelectsScriptedRoster(selfPlayRun: true, workerIndex: 5, hybridScriptedWorkers: 2));
        }

        [Test]
        public void SelfPlay_DefaultZeroK_IsAPureMirrorLeague()
        {
            Assert.IsFalse(TrainingHost.SelectsScriptedRoster(selfPlayRun: true, workerIndex: 0, hybridScriptedWorkers: 0),
                "absent knob must reproduce today's pure self-play behavior");
        }

        [Test]
        public void ScriptedRun_IgnoresTheKnob()
        {
            Assert.IsTrue(TrainingHost.SelectsScriptedRoster(selfPlayRun: false, workerIndex: 3, hybridScriptedWorkers: 0));
            Assert.IsTrue(TrainingHost.SelectsScriptedRoster(selfPlayRun: false, workerIndex: 3, hybridScriptedWorkers: 2),
                "outside self-play every worker is scripted regardless of k");
        }

        [Test]
        public void ResolveHybridScriptedWorkers_AbsentDefaultsToZero()
        {
            Assert.AreEqual(0, TrainingHost.ResolveHybridScriptedWorkers(null));
        }

        [Test]
        public void ResolveHybridScriptedWorkers_ParsesAValidCount()
        {
            Assert.AreEqual(2, TrainingHost.ResolveHybridScriptedWorkers("2"));
        }

        [Test]
        public void ResolveHybridScriptedWorkers_PresentButInvalid_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => TrainingHost.ResolveHybridScriptedWorkers("two"),
                "a malformed count must fail the boot, never silently run a pure mirror league");
            Assert.Throws<InvalidOperationException>(() => TrainingHost.ResolveHybridScriptedWorkers("-1"));
        }
    }
}
#endif

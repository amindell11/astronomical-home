#if UNITY_EDITOR
using Game.RLHarness;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>Pins the per-arena seed decorrelation (--harness-num-arenas): arena 0 is the identity so the M=1 run and every pin/fixture/eval stay byte-identical, and distinct arenas derive distinct, stable seeds layered base → worker → arena — a re-correlated arena silently buys near-duplicate experience.</summary>
    [Category("AI")]
    public class RLArenaSeedEditModeTests
    {
        [Test]
        public void ArenaZero_IsIdentity()
        {
            Assert.AreEqual(EvalProtocol.TrainingRunSeed,
                TrainingHost.DeriveArenaSeed(EvalProtocol.TrainingRunSeed, 0),
                "arena 0 must equal the worker seed so the M=1 run and every pin stay byte-identical");
        }

        [Test]
        public void NonZeroArena_DecorrelatesFromArenaZero()
        {
            var a0 = TrainingHost.DeriveArenaSeed(EvalProtocol.TrainingRunSeed, 0);
            Assert.AreNotEqual(a0, TrainingHost.DeriveArenaSeed(EvalProtocol.TrainingRunSeed, 1),
                "arena 1 must not share arena 0's root seed — that is the duplicate-experience bug this exists to kill");
        }

        [Test]
        public void DistinctArenas_DeriveDistinctSeeds()
        {
            Assert.AreNotEqual(
                TrainingHost.DeriveArenaSeed(EvalProtocol.TrainingRunSeed, 1),
                TrainingHost.DeriveArenaSeed(EvalProtocol.TrainingRunSeed, 2),
                "each fanned-out arena must get its own root seed");
        }

        [Test]
        public void Derivation_IsDeterministic()
        {
            Assert.AreEqual(
                TrainingHost.DeriveArenaSeed(EvalProtocol.TrainingRunSeed, 3),
                TrainingHost.DeriveArenaSeed(EvalProtocol.TrainingRunSeed, 3),
                "the same (workerSeed, arenaIndex) must replay bit-for-bit across calls and processes");
        }

        [Test]
        public void WorkerThenArena_OrderIsPinned()
        {
            var worker1Arena0 = TrainingHost.DeriveArenaSeed(
                TrainingHost.DeriveWorkerSeed(EvalProtocol.TrainingRunSeed, 1), 0);
            var worker0Arena1 = TrainingHost.DeriveArenaSeed(
                TrainingHost.DeriveWorkerSeed(EvalProtocol.TrainingRunSeed, 0), 1);
            Assert.AreNotEqual(worker1Arena0, worker0Arena1,
                "(worker, arena) layering must not commute — a collision would correlate arenas across workers");
        }
    }
}
#endif

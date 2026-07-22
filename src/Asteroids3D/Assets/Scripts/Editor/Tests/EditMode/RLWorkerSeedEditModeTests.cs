#if UNITY_EDITOR
using Game.RLHarness;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>Pins the per-worker seed decorrelation (--num-envs): worker 0 is the identity so every pin/fixture/eval stays byte-identical, and distinct workers derive distinct, stable seeds — a re-correlated worker silently buys near-duplicate experience.</summary>
    [Category("AI")]
    public class RLWorkerSeedEditModeTests
    {
        [Test]
        public void WorkerZero_IsIdentity()
        {
            Assert.AreEqual(EvalProtocol.TrainingRunSeed,
                TrainingHost.DeriveWorkerSeed(EvalProtocol.TrainingRunSeed, 0),
                "worker 0 must equal today's runSeed so the single-env run and every pin stay byte-identical");
        }

        [Test]
        public void NonZeroWorker_DecorrelatesFromWorkerZero()
        {
            var w0 = TrainingHost.DeriveWorkerSeed(EvalProtocol.TrainingRunSeed, 0);
            Assert.AreNotEqual(w0, TrainingHost.DeriveWorkerSeed(EvalProtocol.TrainingRunSeed, 1),
                "worker 1 must not share worker 0's root seed — that is the duplicate-experience bug this exists to kill");
        }

        [Test]
        public void DistinctWorkers_DeriveDistinctSeeds()
        {
            Assert.AreNotEqual(
                TrainingHost.DeriveWorkerSeed(EvalProtocol.TrainingRunSeed, 1),
                TrainingHost.DeriveWorkerSeed(EvalProtocol.TrainingRunSeed, 2),
                "each launched worker must get its own root seed");
        }

        [Test]
        public void Derivation_IsDeterministic()
        {
            Assert.AreEqual(
                TrainingHost.DeriveWorkerSeed(EvalProtocol.TrainingRunSeed, 3),
                TrainingHost.DeriveWorkerSeed(EvalProtocol.TrainingRunSeed, 3),
                "the same (baseSeed, workerIndex) must replay bit-for-bit across calls and processes");
        }
    }
}
#endif

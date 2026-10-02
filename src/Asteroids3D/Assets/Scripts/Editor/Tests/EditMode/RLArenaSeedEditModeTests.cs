#if UNITY_EDITOR
using NUnit.Framework;
using RL.Hosts;

namespace Tests.EditMode
{
    /// <summary>Pins the run-seed decorrelation layered base → worker (--num-envs) → arena (--harness-num-arenas): index 0 of either layer is the identity so the single-env, M=1 run and every pin/fixture/eval stay byte-identical, and distinct indices derive distinct, stable seeds — a re-correlated worker or arena silently buys near-duplicate experience.</summary>
    [Category("AI")]
    public class RLArenaSeedEditModeTests
    {
        public enum Layer { Worker, Arena }

        private static int Derive(Layer layer, int seed, int index) =>
            layer == Layer.Worker
                ? TrainingHost.DeriveWorkerSeed(seed, index)
                : TrainingHost.DeriveArenaSeed(seed, index);

        [TestCase(Layer.Worker, TestName = "WorkerZero_IsIdentity")]
        [TestCase(Layer.Arena, TestName = "ArenaZero_IsIdentity")]
        public void IndexZero_IsIdentity(Layer layer)
        {
            Assert.AreEqual(EvalProtocol.TrainingRunSeed,
                Derive(layer, EvalProtocol.TrainingRunSeed, 0),
                "index 0 must equal the incoming seed so the single-env, M=1 run and every pin stay byte-identical");
        }

        [TestCase(Layer.Worker)]
        [TestCase(Layer.Arena)]
        public void NonZeroIndex_DecorrelatesFromIndexZero(Layer layer)
        {
            var zero = Derive(layer, EvalProtocol.TrainingRunSeed, 0);
            Assert.AreNotEqual(zero, Derive(layer, EvalProtocol.TrainingRunSeed, 1),
                "index 1 must not share index 0's root seed — that is the duplicate-experience bug this exists to kill");
        }

        [TestCase(Layer.Worker)]
        [TestCase(Layer.Arena)]
        public void DistinctIndices_DeriveDistinctSeeds(Layer layer)
        {
            Assert.AreNotEqual(
                Derive(layer, EvalProtocol.TrainingRunSeed, 1),
                Derive(layer, EvalProtocol.TrainingRunSeed, 2),
                "each launched worker and each fanned-out arena must get its own root seed");
        }

        [TestCase(Layer.Worker)]
        [TestCase(Layer.Arena)]
        public void Derivation_IsDeterministic(Layer layer)
        {
            Assert.AreEqual(
                Derive(layer, EvalProtocol.TrainingRunSeed, 3),
                Derive(layer, EvalProtocol.TrainingRunSeed, 3),
                "the same (seed, index) must replay bit-for-bit across calls and processes");
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

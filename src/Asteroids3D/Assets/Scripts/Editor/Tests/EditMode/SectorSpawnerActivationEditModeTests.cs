using System.Collections;
using System.Collections.Generic;
using Game.Sectors;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Sectors.Elements;
using Game.Sectors.Activation;

namespace Tests.EditMode
{
    /// <summary>Token-gated spawner production: empty token produces at Build (the eager default), a set token defers production to exactly one latch, and Teardown disarms the gate.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class SectorSpawnerActivationEditModeTests
    {
        private class CountingSpawner : SectorSpawner
        {
            public int ProduceCalls;
            protected override IEnumerator Produce(SectorBuildContext ctx) { ProduceCalls++; yield break; }
            protected override IEnumerator OnTeardown(SectorBuildContext ctx) { yield break; }
        }

        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            _created.Clear();
        }

        private CountingSpawner NewSpawner(string token = null)
        {
            var go = new GameObject("Spawner");
            _created.Add(go);
            var spawner = go.AddComponent<CountingSpawner>();
            if (token != null) spawner.Configure(token);
            return spawner;
        }

        private static void Drive(IEnumerator it)
        {
            while (it.MoveNext()) { }
        }

        [Test]
        public void EmptyToken_ProducesAtBuild()
        {
            var spawner = NewSpawner();
            Drive(spawner.Build(new SectorBuildContext(null, null, null, null, new SectorEventBus())));
            Assert.AreEqual(1, spawner.ProduceCalls);
        }

        [Test]
        public void Token_DefersProduction_ToExactlyOneLatch()
        {
            var spawner = NewSpawner("go");
            var bus = new SectorEventBus();
            Drive(spawner.Build(new SectorBuildContext(null, null, null, null, bus)));
            Assert.AreEqual(0, spawner.ProduceCalls, "A token-gated spawner must stay dormant at Build.");

            bus.Set("other", true);
            Assert.AreEqual(0, spawner.ProduceCalls, "Unrelated tokens must not produce.");

            bus.Latch("go");
            Assert.AreEqual(1, spawner.ProduceCalls, "The token latch must produce, synchronously.");

            bus.Set("go", false);
            bus.Latch("go");
            Assert.AreEqual(1, spawner.ProduceCalls, "Production happens exactly once.");
        }

        [Test]
        public void TokenAlreadyTrueAtBuild_ProducesImmediately()
        {
            var spawner = NewSpawner("go");
            var bus = new SectorEventBus();
            bus.Latch("go");
            Drive(spawner.Build(new SectorBuildContext(null, null, null, null, bus)));
            Assert.AreEqual(1, spawner.ProduceCalls);
        }

        [Test]
        public void Teardown_DisarmsTheGate()
        {
            var spawner = NewSpawner("go");
            var bus = new SectorEventBus();
            var ctx = new SectorBuildContext(null, null, null, null, bus);
            Drive(spawner.Build(ctx));
            Drive(spawner.Teardown(ctx));

            bus.Latch("go");
            Assert.AreEqual(0, spawner.ProduceCalls, "A torn-down spawner must never produce on a late latch.");
        }

        [Test]
        public void Token_WithNoBus_LogsError_AndStaysInert()
        {
            var spawner = NewSpawner("go");
            LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex("SectorSpawner .*no bus.*inert"));
            Drive(spawner.Build(new SectorBuildContext(null, null, null)));
            Assert.AreEqual(0, spawner.ProduceCalls);
        }
    }
}

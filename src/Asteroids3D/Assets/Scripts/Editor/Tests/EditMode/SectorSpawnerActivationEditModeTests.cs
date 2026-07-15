using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Sectors;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode
{
    /// <summary>Spawner activation modes: Eager produces at Build, Gated defers production to exactly one latch of its signal, Gated+null is loud inert (null never means eager), and Teardown disarms the gate.</summary>
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

        private CountingSpawner NewSpawner()
        {
            var go = new GameObject("Spawner");
            _created.Add(go);
            return go.AddComponent<CountingSpawner>();
        }

        private SignalPort NewPort(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go.AddComponent<SignalPort>();
        }

        private static void Drive(IEnumerator it)
        {
            while (it.MoveNext()) { }
        }

        [Test]
        public void Eager_ProducesAtBuild()
        {
            var spawner = NewSpawner();
            Drive(spawner.Build(new SectorBuildContext(null, null, null, new SectorEventBus())));
            Assert.AreEqual(1, spawner.ProduceCalls);
        }

        [Test]
        public void Gated_DefersProduction_ToExactlyOneLatch()
        {
            var spawner = NewSpawner();
            var go = NewPort("go");
            var other = NewPort("other");
            spawner.ConfigureGated(go);
            var bus = new SectorEventBus();
            Drive(spawner.Build(new SectorBuildContext(null, null, null, bus)));
            Assert.AreEqual(0, spawner.ProduceCalls, "A gated spawner must stay dormant at Build.");

            bus.Set(other, true);
            Assert.AreEqual(0, spawner.ProduceCalls, "Unrelated ports must not produce.");

            bus.Latch(go);
            Assert.AreEqual(1, spawner.ProduceCalls, "The signal latch must produce, synchronously.");

            bus.Set(go, false);
            bus.Latch(go);
            Assert.AreEqual(1, spawner.ProduceCalls, "Production happens exactly once.");
        }

        [Test]
        public void Gated_SignalAlreadyTrueAtBuild_ProducesImmediately()
        {
            var spawner = NewSpawner();
            var go = NewPort("go");
            spawner.ConfigureGated(go);
            var bus = new SectorEventBus();
            bus.Latch(go);
            Drive(spawner.Build(new SectorBuildContext(null, null, null, bus)));
            Assert.AreEqual(1, spawner.ProduceCalls);
        }

        [Test]
        public void Teardown_DisarmsTheGate()
        {
            var spawner = NewSpawner();
            var go = NewPort("go");
            spawner.ConfigureGated(go);
            var bus = new SectorEventBus();
            var ctx = new SectorBuildContext(null, null, null, bus);
            Drive(spawner.Build(ctx));
            Drive(spawner.Teardown(ctx));

            bus.Latch(go);
            Assert.AreEqual(0, spawner.ProduceCalls, "A torn-down spawner must never produce on a late latch.");
        }

        [Test]
        public void Gated_WithNullSignal_LogsError_AndStaysInert()
        {
            var spawner = NewSpawner();
            spawner.ConfigureGated(null);
            var bus = new SectorEventBus();

            LogAssert.Expect(LogType.Error, new Regex("CountingSpawner .*unassigned activation port.*inert"));
            Drive(spawner.Build(new SectorBuildContext(null, null, null, bus)));

            Assert.AreEqual(0, spawner.ProduceCalls, "Gated with a null signal must be loud-inert — null never means eager.");
        }

        [Test]
        public void ConfigureEager_AfterGated_ProducesAtBuild()
        {
            var spawner = NewSpawner();
            spawner.ConfigureGated(NewPort("go"));
            spawner.ConfigureEager();
            Drive(spawner.Build(new SectorBuildContext(null, null, null, new SectorEventBus())));
            Assert.AreEqual(1, spawner.ProduceCalls);
        }

        [Test]
        public void Gated_WithNoBus_LogsError_AndStaysInert()
        {
            var spawner = NewSpawner();
            spawner.ConfigureGated(NewPort("go"));
            LogAssert.Expect(LogType.Error, new Regex("CountingSpawner .*no bus.*inert"));
            Drive(spawner.Build(new SectorBuildContext(null, null)));
            Assert.AreEqual(0, spawner.ProduceCalls);
        }
    }
}

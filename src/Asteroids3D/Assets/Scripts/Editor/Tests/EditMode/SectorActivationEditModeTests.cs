using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Sectors;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode
{
    /// <summary>EditMode tests for the activation substrate: SectorEventBus + ActivationPredicate driven directly, ActivationRule stepped without a sector.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class SectorActivationEditModeTests
    {
        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            _created.Clear();
        }

        private GameObject NewGO(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        private SignalPort NewPort(string name) => NewGO(name).AddComponent<SignalPort>();

        private static void Run(IEnumerator routine)
        {
            while (routine.MoveNext()) { }
        }

        private class LoggingRule : ActivationRule
        {
            public List<string> Log;
            public string Id;
            protected override void OnFired() => Log.Add($"{Id}:effect");
        }

        [Test]
        public void SignalAndSignal_FiresOnce_WhenBothHold()
        {
            var bus = new SectorEventBus();
            var a = NewPort("a");
            var b = NewPort("b");
            var predicate = new ActivationPredicate(new[]
                { ActivationTerm.Signal(a), ActivationTerm.Signal(b) });

            Assert.IsFalse(predicate.Evaluate(bus, 0f));

            bus.Set(a, true);
            Assert.IsFalse(predicate.Evaluate(bus, 0f), "A single held term must not satisfy an AND of two.");

            bus.Set(b, true);
            Assert.IsTrue(predicate.Evaluate(bus, 0f), "Both terms holding must fire the predicate.");
            Assert.IsTrue(predicate.Satisfied);
            Assert.IsFalse(predicate.Evaluate(bus, 0f), "A satisfied predicate must never fire again.");
        }

        [Test]
        public void ParkedThenQualified_LevelAlreadyTrue_Fires()
        {
            var bus = new SectorEventBus();
            var inZone = NewPort("in-zone");
            var keyAcquired = NewPort("key-acquired");
            var predicate = new ActivationPredicate(new[]
                { ActivationTerm.Signal(inZone), ActivationTerm.Signal(keyAcquired) });

            bus.Set(inZone, true);
            Assert.IsFalse(predicate.Evaluate(bus, 0f));

            bus.Latch(keyAcquired);
            Assert.IsTrue(predicate.Evaluate(bus, 0f),
                "A level held BEFORE the other term satisfies must count — standing predicate, not enter-edge.");
        }

        [Test]
        public void Latch_AfterFiring_DropAndReraiseTerm_DoesNotRefire()
        {
            var bus = new SectorEventBus();
            var inZone = NewPort("in-zone");
            var predicate = new ActivationPredicate(new[] { ActivationTerm.Signal(inZone) });

            bus.Set(inZone, true);
            Assert.IsTrue(predicate.Evaluate(bus, 0f));

            bus.Set(inZone, false);
            bus.Set(inZone, true);
            Assert.IsFalse(predicate.Evaluate(bus, 0f), "Leave-and-re-enter must not re-arm or re-fire.");
            Assert.IsTrue(predicate.Satisfied);
        }

        [Test]
        public void EventTerm_LatchedPort_SatisfiesPermanently()
        {
            var bus = new SectorEventBus();
            var boom = NewPort("boom");
            var term = ActivationTerm.Signal(boom);

            bus.Latch(boom);
            Assert.IsTrue(term.IsSatisfied(bus, 0f));

            bus.Set(boom, false);
            Assert.IsTrue(bus.Get(boom), "A latched port must never clear.");
            Assert.IsTrue(term.IsSatisfied(bus, 0f));
        }

        [Test]
        public void TimeTerm_SatisfiesAtThreshold()
        {
            var bus = new SectorEventBus();
            var predicate = new ActivationPredicate(new[] { ActivationTerm.Time(5f) });

            Assert.IsFalse(predicate.Evaluate(bus, 4.999f));
            Assert.IsTrue(predicate.Evaluate(bus, 5f), "A time term must satisfy at exactly its threshold.");
        }

        [Test]
        public void Chaining_RuleAFiredPort_SatisfiesRuleB_AndRulesFireOnce()
        {
            var bus = new SectorEventBus();
            var ctx = new SectorBuildContext(null, null, null, bus);
            var go = NewPort("go");
            var aPort = NewPort("a-fired");
            var bPort = NewPort("b-fired");

            var a = NewGO("RuleA").AddComponent<ActivationRule>();
            a.Configure(new[] { ActivationTerm.Signal(go) }, aPort);
            var b = NewGO("RuleB").AddComponent<ActivationRule>();
            b.Configure(new[] { ActivationTerm.Signal(aPort) }, bPort);

            Run(a.Setup(ctx));
            Run(b.Setup(ctx));

            var aFired = 0;
            var bFired = 0;
            a.Fired += () => aFired++;
            b.Fired += () => bFired++;

            bus.Set(go, true);
            Assert.AreEqual(1, aFired, "Rule A must fire when its term holds.");
            Assert.AreEqual(1, bFired, "Rule A's latched fired port must chain into rule B's signal term.");
            Assert.IsTrue(bus.Get(aPort));

            bus.Set(go, false);
            bus.Set(go, true);
            Assert.AreEqual(1, aFired, "A fired rule must stay fired across term churn.");
            Assert.AreEqual(1, bFired);

            Run(a.Teardown(ctx));
            Run(b.Teardown(ctx));
        }

        [Test]
        public void CausalOrder_EffectThenEvent_BeforeDownstreamRule()
        {
            var bus = new SectorEventBus();
            var ctx = new SectorBuildContext(null, null, null, bus);
            var log = new List<string>();
            var go = NewPort("go");
            var aPort = NewPort("a-fired");
            var bPort = NewPort("b-fired");

            var a = NewGO("RuleA").AddComponent<LoggingRule>();
            a.Log = log;
            a.Id = "A";
            a.Configure(new[] { ActivationTerm.Signal(go) }, aPort);
            a.Fired += () => log.Add("A:event");

            var b = NewGO("RuleB").AddComponent<LoggingRule>();
            b.Log = log;
            b.Id = "B";
            b.Configure(new[] { ActivationTerm.Signal(aPort) }, bPort);
            b.Fired += () => log.Add("B:event");

            Run(a.Setup(ctx));
            Run(b.Setup(ctx));

            bus.Set(go, true);

            CollectionAssert.AreEqual(new[] { "A:effect", "A:event", "B:effect", "B:event" }, log,
                "Fire order must be OnFired → Fired → latch, so A's effect completes before B runs.");
        }

        [Test]
        public void FrozenBus_SetAndLatch_AreNoOps_AndNeverRaiseChanged()
        {
            var bus = new SectorEventBus();
            var x = NewPort("x");
            var y = NewPort("y");
            var z = NewPort("z");
            bus.Set(x, true);
            bus.Freeze();

            var changes = 0;
            bus.Changed += _ => changes++;

            bus.Set(x, false);
            bus.Set(y, true);
            bus.Latch(z);

            Assert.AreEqual(0, changes, "A frozen bus must never raise Changed.");
            Assert.IsTrue(bus.Get(x), "Pre-freeze values must stay readable.");
            Assert.IsFalse(bus.Get(y));
            Assert.IsFalse(bus.Get(z));
        }

        [Test]
        public void UnassignedFiredPort_RuleLogsError_AndStaysInert()
        {
            var bus = new SectorEventBus();
            var ctx = new SectorBuildContext(null, null, null, bus);
            var go = NewPort("go");
            var rule = NewGO("BadPublishRule").AddComponent<ActivationRule>();
            rule.Configure(new[] { ActivationTerm.Signal(go) });

            LogAssert.Expect(LogType.Error, new Regex("ActivationRule .*BadPublishRule.*unassigned fired port.*inert"));
            Run(rule.Setup(ctx));

            var fired = 0;
            rule.Fired += () => fired++;
            bus.Set(go, true);

            Assert.AreEqual(0, fired, "An invalid rule must be fully inert, not half-working.");
            Assert.IsFalse(rule.HasFired);
        }

        [Test]
        public void UnassignedSignalTermPort_RuleLogsError_AndStaysInert()
        {
            var bus = new SectorEventBus();
            var ctx = new SectorBuildContext(null, null, null, bus);
            var rule = NewGO("BadTermRule").AddComponent<ActivationRule>();
            rule.Configure(new[] { ActivationTerm.Signal(null), ActivationTerm.Time(0f) }, NewPort("fired"));

            LogAssert.Expect(LogType.Error, new Regex("ActivationRule .*BadTermRule.*unassigned term port.*inert"));
            Run(rule.Setup(ctx));

            Assert.IsFalse(rule.HasFired, "An invalid rule must not arm at Setup.");
        }

        [Test]
        public void CrossSectorTermPort_RuleLogsError_AndStaysInert()
        {
            var bus = new SectorEventBus();
            var home = NewGO("HomeSector").AddComponent<Sector>();
            var foreign = NewGO("ForeignSector").AddComponent<Sector>();
            var foreignPort = foreign.gameObject.AddComponent<SignalPort>();

            var ruleGO = NewGO("Rule");
            ruleGO.transform.SetParent(home.transform);
            var homePort = ruleGO.AddComponent<SignalPort>();
            var rule = ruleGO.AddComponent<ActivationRule>();
            rule.Configure(new[] { ActivationTerm.Signal(foreignPort) }, homePort);

            LogAssert.Expect(LogType.Error, new Regex("ActivationRule .*outside its own sector.*inert"));
            Run(rule.Setup(new SectorBuildContext(null, home, null, bus)));

            bus.Set(foreignPort, true);
            Assert.IsFalse(rule.HasFired, "A rule wired across sectors through a code seam must be inert.");
        }

        [Test]
        public void DelayedRule_OnInactiveGameObject_LogsError_AndStaysInert()
        {
            var bus = new SectorEventBus();
            var ctx = new SectorBuildContext(null, null, null, bus);
            var go = NewPort("go");
            var ruleGO = NewGO("DormantDelayedRule");
            var rule = ruleGO.AddComponent<ActivationRule>();
            rule.Configure(new[] { ActivationTerm.Signal(go) }, NewPort("fired"), fireDelaySeconds: 1f);
            ruleGO.SetActive(false);

            LogAssert.Expect(LogType.Error, new Regex("ActivationRule .*delayed fire.*inactive.*inert"));
            Run(rule.Setup(ctx));

            bus.Set(go, true);
            Assert.IsFalse(rule.HasFired, "A delayed rule needs Update — inactive means its fire could never run.");
        }

        [Test]
        public void InactiveTriggerVolume_LogsError_AndStaysInert()
        {
            var bus = new SectorEventBus();
            var ctx = new SectorBuildContext(null, null, null, bus);
            var volumeGO = NewGO("DormantVolume");
            volumeGO.AddComponent<SphereCollider>().isTrigger = true;
            var volume = volumeGO.AddComponent<TriggerVolume>();
            volume.Configure(NewPort("inside"));
            volumeGO.SetActive(false);

            LogAssert.Expect(LogType.Error, new Regex("TriggerVolume .*trigger messages.*inactive.*inert"));
            Run(volume.Setup(ctx));
        }

        [Test]
        public void BusChanged_RaisedOnlyOnActualValueChanges()
        {
            var bus = new SectorEventBus();
            var x = NewPort("x");
            var changes = new List<SignalPort>();
            bus.Changed += changes.Add;

            bus.Set(x, false);
            Assert.IsEmpty(changes, "Set(false) on an unset port must not raise Changed.");

            bus.Set(x, true);
            CollectionAssert.AreEqual(new[] { x }, changes);

            bus.Set(x, true);
            CollectionAssert.AreEqual(new[] { x }, changes, "Re-setting the same value must not raise Changed.");

            bus.Set(x, false);
            CollectionAssert.AreEqual(new[] { x, x }, changes);

            bus.Latch(x);
            CollectionAssert.AreEqual(new[] { x, x, x }, changes);

            bus.Latch(x);
            bus.Set(x, false);
            CollectionAssert.AreEqual(new[] { x, x, x }, changes,
                "Re-latching or Set(false) on a latched port must not raise Changed.");
            Assert.IsTrue(bus.Get(x));
        }

        [Test]
        public void Bus_NullPort_ReadsFalse_AndWritesAreNoOps()
        {
            var bus = new SectorEventBus();
            var changes = 0;
            bus.Changed += _ => changes++;

            bus.Set(null, true);
            bus.Latch(null);

            Assert.IsFalse(bus.Get(null));
            Assert.AreEqual(0, changes, "A null port must never enter the bus.");
        }
    }
}

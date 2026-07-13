using System.Collections;
using System.Collections.Generic;
using Game.Sectors;
using NUnit.Framework;
using UnityEngine;

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

        private static void Run(IEnumerator routine)
        {
            while (routine.MoveNext()) { }
        }

        [Test]
        public void SignalAndSignal_FiresOnce_WhenBothHold()
        {
            var bus = new SectorEventBus();
            var predicate = new ActivationPredicate(new[]
                { ActivationTerm.Signal("a"), ActivationTerm.Signal("b") });

            Assert.IsFalse(predicate.Evaluate(bus, 0f));

            bus.Set("a", true);
            Assert.IsFalse(predicate.Evaluate(bus, 0f), "A single held term must not satisfy an AND of two.");

            bus.Set("b", true);
            Assert.IsTrue(predicate.Evaluate(bus, 0f), "Both terms holding must fire the predicate.");
            Assert.IsTrue(predicate.Satisfied);
            Assert.IsFalse(predicate.Evaluate(bus, 0f), "A satisfied predicate must never fire again.");
        }

        [Test]
        public void ParkedThenQualified_LevelAlreadyTrue_Fires()
        {
            var bus = new SectorEventBus();
            var predicate = new ActivationPredicate(new[]
                { ActivationTerm.Signal("in-zone"), ActivationTerm.Signal("key-acquired") });

            bus.Set("in-zone", true);
            Assert.IsFalse(predicate.Evaluate(bus, 0f));

            bus.Latch("key-acquired");
            Assert.IsTrue(predicate.Evaluate(bus, 0f),
                "A level held BEFORE the other term satisfies must count — standing predicate, not enter-edge.");
        }

        [Test]
        public void Latch_AfterFiring_DropAndReraiseTerm_DoesNotRefire()
        {
            var bus = new SectorEventBus();
            var predicate = new ActivationPredicate(new[] { ActivationTerm.Signal("in-zone") });

            bus.Set("in-zone", true);
            Assert.IsTrue(predicate.Evaluate(bus, 0f));

            bus.Set("in-zone", false);
            bus.Set("in-zone", true);
            Assert.IsFalse(predicate.Evaluate(bus, 0f), "Leave-and-re-enter must not re-arm or re-fire.");
            Assert.IsTrue(predicate.Satisfied);
        }

        [Test]
        public void EventTerm_LatchedToken_SatisfiesPermanently()
        {
            var bus = new SectorEventBus();
            var term = ActivationTerm.Signal("boom");

            bus.Latch("boom");
            Assert.IsTrue(term.IsSatisfied(bus, 0f));

            bus.Set("boom", false);
            Assert.IsTrue(bus.Get("boom"), "A latched token must never clear.");
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
        public void Chaining_RuleAPublishOnFired_SatisfiesRuleB_AndRulesFireOnce()
        {
            var bus = new SectorEventBus();
            var ctx = new SectorBuildContext(null, null, null, bus);

            var a = NewGO("RuleA").AddComponent<ActivationRule>();
            a.Configure(new[] { ActivationTerm.Signal("go") }, new[] { "a-fired" });
            var b = NewGO("RuleB").AddComponent<ActivationRule>();
            b.Configure(new[] { ActivationTerm.Signal("a-fired") });

            Run(a.Setup(ctx));
            Run(b.Setup(ctx));

            var aFired = 0;
            var bFired = 0;
            a.Fired += () => aFired++;
            b.Fired += () => bFired++;

            bus.Set("go", true);
            Assert.AreEqual(1, aFired, "Rule A must fire when its term holds.");
            Assert.AreEqual(1, bFired, "Rule A's published token must chain into rule B's signal term.");
            Assert.IsTrue(bus.Get("a-fired"));

            bus.Set("go", false);
            bus.Set("go", true);
            Assert.AreEqual(1, aFired, "A fired rule must stay fired across term churn.");
            Assert.AreEqual(1, bFired);

            Run(a.Teardown(ctx));
            Run(b.Teardown(ctx));
        }

        [Test]
        public void BusChanged_RaisedOnlyOnActualValueChanges()
        {
            var bus = new SectorEventBus();
            var changes = new List<string>();
            bus.Changed += changes.Add;

            bus.Set("x", false);
            Assert.IsEmpty(changes, "Set(false) on an unset token must not raise Changed.");

            bus.Set("x", true);
            CollectionAssert.AreEqual(new[] { "x" }, changes);

            bus.Set("x", true);
            CollectionAssert.AreEqual(new[] { "x" }, changes, "Re-setting the same value must not raise Changed.");

            bus.Set("x", false);
            CollectionAssert.AreEqual(new[] { "x", "x" }, changes);

            bus.Latch("x");
            CollectionAssert.AreEqual(new[] { "x", "x", "x" }, changes);

            bus.Latch("x");
            bus.Set("x", false);
            CollectionAssert.AreEqual(new[] { "x", "x", "x" }, changes,
                "Re-latching or Set(false) on a latched token must not raise Changed.");
            Assert.IsTrue(bus.Get("x"));
        }
    }
}

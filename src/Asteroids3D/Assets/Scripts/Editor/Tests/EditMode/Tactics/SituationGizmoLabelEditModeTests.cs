#if UNITY_EDITOR
using AI;
using AI.Tactics;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Tactics
{
    [Category("AI")]
    public class SituationGizmoLabelEditModeTests
    {
        private TacticsSettings temper;

        [SetUp]
        public void SetUp()
        {
            temper = ScriptableObject.CreateInstance<TacticsSettings>();
            temper.name = "KiterTemper";
            temper.wounded = WoundedResponse.BreakOff;
            temper.commits = false;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(temper);

        [Test]
        public void HealthyShip_NamesTemperAndTactic()
        {
            temper.commits = true;

            var text = SituationGizmos.Label(temper.name, Tactic.FallBack, temper, wounded: false,
                respondingSeconds: null, retreatsTaken: 0, draw: new WoundedDraw(25f, 80f, 5f, 3));

            Assert.AreEqual("Kiter · Fall Back", text, "a ship that is not wounded names its temper and its tactic");
        }

        [Test]
        public void WoundedAndWaiting_SaysWaitingToRetreat()
        {
            temper.wounded = WoundedResponse.FallBack;

            var text = SituationGizmos.Label(temper.name, Tactic.AttackRun, temper, wounded: true,
                respondingSeconds: null, retreatsTaken: 0, draw: new WoundedDraw(25f, 80f, 5f, 3));

            Assert.AreEqual("Kiter · Attack Run\nwounded · waiting to retreat", text);
        }

        [Test]
        public void WoundedAndRetreating_OnACommittingTemper_ShowsSecondsAndRetreatsLeft()
        {
            temper.wounded = WoundedResponse.BreakOff;
            temper.commits = true;

            var text = SituationGizmos.Label(temper.name, Tactic.BreakOff, temper, wounded: true,
                respondingSeconds: 3.2f, retreatsTaken: 1, draw: new WoundedDraw(25f, 80f, 5f, 3));

            Assert.AreEqual("Kiter · Break Off\nwounded · retreating 3.2 s · 2 of 3 left", text);
        }

        [Test]
        public void WoundedAndCautious_ShowsCautiousSeconds()
        {
            temper.wounded = WoundedResponse.TurnCautious;

            var text = SituationGizmos.Label(temper.name, Tactic.Flank, temper, wounded: true,
                respondingSeconds: 1.4f, retreatsTaken: 0, draw: null);

            Assert.AreEqual("Kiter · Flank\nwounded · cautious 1.4 s", text);
        }

        [Test]
        public void RetreatsSpent_OnACommittingTemper_SaysCommitted()
        {
            temper.commits = true;

            var text = SituationGizmos.Label(temper.name, Tactic.AttackRun, temper, wounded: false,
                respondingSeconds: null, retreatsTaken: 2, draw: new WoundedDraw(25f, 80f, 5f, 2));

            Assert.AreEqual("Kiter · Attack Run\ncommitted", text);
        }

        [Test]
        public void NoTemper_ShowsTheTacticAlone()
        {
            var text = SituationGizmos.Label(null, Tactic.FallBack, null, wounded: true,
                respondingSeconds: 3.2f, retreatsTaken: 1, draw: new WoundedDraw(25f, 80f, 5f, 3));

            Assert.AreEqual("Fall Back", text);
        }
    }
}
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Objectives;
using Substrate.Sectors.Activation;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Phases;
using Substrate.Services.Objectives;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>PhaseSpineModule against the objective service: the survive step reads the director's phase into its status line, extraction opens only once the phases are done (zone shown, armed and targeted), and teardown closes the spine.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class PhaseSpineModuleEditModeTests
    {
        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
                if (go) UnityEngine.Object.DestroyImmediate(go);
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

        private (PhaseSpineModule module, WaveDirector director, ExtractionZone zone, ObjectiveService svc, SectorBuildContext ctx)
            Build()
        {
            var svc = NewGO("ObjectiveService").AddComponent<ObjectiveService>();

            var director = NewGO("WaveDirector").AddComponent<WaveDirector>();
            director.Configure(null, new[]
            {
                new SectorPhase { seconds = 40f, interval = 6f, cap = 2 },
                new SectorPhase { seconds = 40f, interval = 4f, cap = 3 },
            }, lullSeconds: 5f);
            director.BeginSchedule(0f);

            var zoneGO = NewGO("Zone");
            zoneGO.AddComponent<SphereCollider>().isTrigger = true;
            var zone = zoneGO.AddComponent<ExtractionZone>();

            var module = NewGO("Spine").AddComponent<PhaseSpineModule>();
            module.Bind(director, zone);

            var ctx = new SectorBuildContext(null, svc, true, null, default, null, null, bus: new SectorEventBus());
            return (module, director, zone, svc, ctx);
        }

        [Test]
        public void Setup_StartsInSurvive_WithThePhaseLine_AndTheZoneHidden()
        {
            var (module, director, zone, svc, ctx) = Build();

            Run(module.Setup(ctx));

            Assert.AreEqual(PhaseSpineModule.StepSurvive, svc.SpineStep);
            Assert.AreEqual("PHASE 1/2    0:40", svc.SpineTracker.CurrentStatusLine);
            Assert.IsFalse(zone.gameObject.activeSelf, "The zone stays hidden until extraction opens.");
            Assert.IsNull(svc.SpineTarget);

            director.Advance(41.5f);
            Assert.AreEqual("PHASE 2/2 IN 0:04", svc.SpineTracker.CurrentStatusLine, "A lull counts down to the phase it leads into.");
        }

        [Test]
        public void PhasesDone_OpensExtraction_ShowingArmingAndTargetingTheZone()
        {
            var (module, director, zone, svc, ctx) = Build();
            Run(module.Setup(ctx));

            director.Advance(60f);
            svc.Tick(0.1f);
            Assert.AreEqual(PhaseSpineModule.StepSurvive, svc.SpineStep, "Mid-table, the spine is still surviving.");

            director.Advance(85f);
            svc.Tick(0.1f);
            Assert.AreEqual(PhaseSpineModule.StepExtract, svc.SpineStep);
            Assert.AreEqual(ObjectiveType.ExtractionChallenge, svc.SpineState, "The minimap marker shows targets in this state.");
            Assert.IsTrue(zone.gameObject.activeSelf);
            Assert.AreEqual(zone.transform, svc.SpineTarget);
            Assert.AreEqual("REACH THE JUMP GATE", svc.SpineTracker.CurrentStatusLine);
        }

        [Test]
        public void Teardown_ClosesTheSpine()
        {
            var (module, _, _, svc, ctx) = Build();
            Run(module.Setup(ctx));

            Run(module.Teardown(ctx));

            Assert.IsNull(svc.SpineTracker);
        }

        [Test]
        public void Setup_WithoutItsDirectorOrZone_Throws()
        {
            var (module, director, _, _, ctx) = Build();
            module.Bind(director, null);

            Assert.Throws<InvalidOperationException>(() => Run(module.Setup(ctx)));
        }
    }
}

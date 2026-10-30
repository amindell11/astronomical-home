using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Objectives;
using Ships;
using Substrate;
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

        private (PhaseSpineModule module, WaveDirector director, ExtractionZone zone, ExtractionZone beacon,
            ObjectiveService svc, SectorBuildContext ctx) Build(Ship hero = null)
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

            var beaconGO = NewGO("Beacon");
            beaconGO.AddComponent<SphereCollider>().isTrigger = true;
            var beacon = beaconGO.AddComponent<ExtractionZone>();

            var module = NewGO("Spine").AddComponent<PhaseSpineModule>();
            module.Bind(director, zone, beacon);

            var ctx = new SectorBuildContext(null, svc, true, null, default, null, hero, bus: new SectorEventBus());
            return (module, director, zone, beacon, svc, ctx);
        }

        [Test]
        public void Setup_StartsInSurvive_WithThePhaseLine_AndTheZoneHidden()
        {
            var (module, director, zone, _, svc, ctx) = Build();

            Run(module.Setup(ctx));

            Assert.AreEqual(PhaseSpineModule.StepSurvive, svc.SpineStep);
            Assert.AreEqual("PHASE 1/2    0:40", svc.SpineTracker.CurrentStatusLine);
            Assert.AreEqual(45f, svc.SpineTracker.PhaseProgress.Value.SecondsLeft);
            Assert.AreEqual(2, svc.SpineTracker.PhaseProgress.Value.Count);
            Assert.IsFalse(zone.gameObject.activeSelf, "The zone stays hidden until extraction opens.");
            Assert.IsNull(svc.SpineTarget);

            director.Advance(41.5f);
            Assert.AreEqual("PHASE 2/2 IN 0:04", svc.SpineTracker.CurrentStatusLine, "A lull counts down to the phase it leads into.");
            Assert.AreEqual(0, svc.SpineTracker.PhaseProgress.Value.Index);
            Assert.AreEqual(3.5f, svc.SpineTracker.PhaseProgress.Value.SecondsLeft);
        }

        [Test]
        public void PhasesDone_OpensExtraction_ShowingArmingAndTargetingTheZone()
        {
            var (module, director, zone, _, svc, ctx) = Build();
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
            Assert.AreEqual(string.Empty, svc.SpineTracker.CurrentStatusLine);
            Assert.IsTrue(svc.SpineTracker.PhaseProgress.Value.ExtractionOpen);
        }

        [Test]
        public void Lull_WithAHero_OpensABeaconAwayFromTheHero_AndTargetsIt()
        {
            var hero = NewGO("Hero").AddComponent<Ship>();
            var (module, director, zone, beacon, svc, ctx) = Build(hero);
            Run(module.Setup(ctx));

            director.Advance(41.5f);
            module.Tick();

            Assert.IsTrue(beacon.gameObject.activeSelf);
            Assert.AreEqual(beacon.transform, svc.SpineTarget);
            var distance = (GamePlane.WorldPointToPlane(beacon.transform.position)
                - GamePlane.WorldPointToPlane(hero.transform.position)).magnitude;
            Assert.AreEqual(150f, distance, 0.01f);
            Assert.IsFalse(module.BeaconClaimed);
            Assert.IsFalse(zone.gameObject.activeSelf, "The extraction zone stays hidden through a lull.");
        }

        [Test]
        public void LullEnd_ClosesAnUnclaimedBeacon()
        {
            var (module, director, _, beacon, svc, ctx) = Build(NewGO("Hero").AddComponent<Ship>());
            Run(module.Setup(ctx));
            director.Advance(41.5f);
            module.Tick();

            director.Advance(46f);
            module.Tick();

            Assert.IsFalse(beacon.gameObject.activeSelf);
            Assert.IsNull(svc.SpineTarget);
            Assert.IsFalse(module.BeaconClaimed);
        }

        [Test]
        public void Lull_WithoutAHero_OpensNoBeacon()
        {
            var (module, director, _, beacon, svc, ctx) = Build();
            Run(module.Setup(ctx));

            director.Advance(41.5f);
            module.Tick();

            Assert.IsFalse(beacon.gameObject.activeSelf);
            Assert.IsNull(svc.SpineTarget);
        }

        [Test]
        public void Teardown_ClosesTheSpine()
        {
            var (module, _, _, _, svc, ctx) = Build();
            Run(module.Setup(ctx));

            Run(module.Teardown(ctx));

            Assert.IsNull(svc.SpineTracker);
        }

        [Test]
        public void Setup_WithoutItsDirectorOrZone_Throws()
        {
            var (module, director, _, beacon, _, ctx) = Build();
            module.Bind(director, null, beacon);

            Assert.Throws<InvalidOperationException>(() => Run(module.Setup(ctx)));
        }

        [Test]
        public void Setup_WithoutItsBeaconZone_Throws()
        {
            var (module, director, zone, _, _, ctx) = Build();
            module.Bind(director, zone, null);

            Assert.Throws<InvalidOperationException>(() => Run(module.Setup(ctx)));
        }
    }
}

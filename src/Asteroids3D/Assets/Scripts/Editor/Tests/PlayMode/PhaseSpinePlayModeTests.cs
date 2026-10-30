#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Objectives;
using Ships;
using Substrate;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Phases;
using Substrate.Services;
using Substrate.Services.Objectives;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode
{
    /// <summary>A phased sector end to end: the director's phases run out, the extraction zone appears at its distance from the hero, and a non-player hero's jump request inside ends the sector extracted; a lull's beacon appears at its distance and flying in claims it.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class PhaseSpinePlayModeTests : PlayModeWorldFixture
    {
        private const float ZoneDistance = 150f;
        private const float BeaconDistance = 150f;

        private UnitService _unitService;
        private ObjectiveService _objectives;
        private readonly List<GameObject> _created = new();

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            var unitServiceGO = TrackGO(new GameObject("UnitService"));
            _unitService = unitServiceGO.AddComponent<UnitService>();

            var objectiveServiceGO = TrackGO(new GameObject("ObjectiveService"));
            _objectives = objectiveServiceGO.AddComponent<ObjectiveService>();

            ShipServices.Compose(_unitService, unitServiceGO.transform, presentationEnabled: true);
        }

        [TearDown]
        public override void TearDown()
        {
            if (_unitService) _unitService.Clear();

            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            _created.Clear();

            base.TearDown();
        }

        private GameObject TrackGO(GameObject go) { _created.Add(go); return go; }

        // Primitive test ship, not Ship_2: its layer-7 collider needs LFS geometry.
        private Ship SpawnKinematicShip(Vector2 planePos) =>
            TrackGO(ShipTestFactory.CreateKinematicPrimitiveShipAt(planePos).gameObject).GetComponent<Ship>();

        private static ExtractionZone NewZone(Transform sector, string name)
        {
            var zoneGO = new GameObject(name);
            zoneGO.transform.SetParent(sector, false);
            var collider = zoneGO.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 4f;
            return zoneGO.AddComponent<ExtractionZone>();
        }

        private static IEnumerator WaitSeconds(Func<bool> done, float seconds)
        {
            var deadline = Time.time + seconds;
            while (!done() && Time.time < deadline)
                yield return null;
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator PhasesRunOut_ZoneAppearsAway_AndARequestInsideExtracts()
        {
            var player = SpawnKinematicShip(Vector2.zero);
            var pilot = JumpTestCommander.Fly(player);
            var template = SpawnKinematicShip(new Vector2(500f, 500f));
            template.gameObject.SetActive(false);

            var sectorGO = TrackGO(new GameObject("PhasedSector"));
            var sector = sectorGO.AddComponent<Sector>();

            var directorGO = new GameObject("WaveDirector");
            directorGO.transform.SetParent(sectorGO.transform, false);
            var director = directorGO.AddComponent<WaveDirector>();
            director.Configure(new[] { new WaveDirector.RosterEntry { template = template } },
                new[] { new SectorPhase { seconds = 1f, interval = 1f, cap = 0 } }, lullSeconds: 0f);

            var zone = NewZone(sectorGO.transform, "Zone");

            var spine = sectorGO.AddComponent<PhaseSpineModule>();
            spine.Bind(director, zone, NewZone(sectorGO.transform, "Beacon"));

            sector.SetManifest(null, new SectorSpawner[] { director }, new SectorModule[] { spine });
            sector.Initialize(_unitService, _objectives, true, default, player);
            SectorResult? got = null;
            ((ISector)sector).OnSectorComplete += r => got = r;

            yield return sector.Setup();
            Assert.AreEqual(PhaseSpineModule.StepSurvive, _objectives.SpineStep);
            Assert.IsFalse(zone.gameObject.activeSelf, "The zone stays hidden while the phases run.");

            yield return WaitSeconds(() => _objectives.SpineStep == PhaseSpineModule.StepExtract, 5f);
            Assert.AreEqual(PhaseSpineModule.StepExtract, _objectives.SpineStep,
                "The spine opens extraction once the phases run out.");
            Assert.IsTrue(zone.gameObject.activeSelf);
            Assert.AreEqual(ZoneDistance,
                Vector2.Distance(GamePlane.WorldPointToPlane(zone.transform.position), Vector2.zero), 0.01f,
                "The zone appears its distance from the player.");

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(zone.IsPlayerInZone, "The relocated zone has settled outside the player.");
            pilot.RequestJump();
            Assert.IsFalse(got.HasValue, "A request outside the zone must not extract.");
            ShipTestFactory.MoveKinematicShip(player, zone.transform.position);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(got.HasValue, "Entering after a dropped request must not extract.");
            Assert.AreEqual("PRESS F TO JUMP", _objectives.SpineTracker.CurrentStatusLine);
            pilot.RequestJump();

            Assert.IsTrue(got.HasValue, "A request inside must end the sector on the spot.");
            Assert.IsTrue(got.Value.Success, "A request in the eligible zone ends the sector extracted.");
            Assert.AreEqual(zone.DepartureHeading, got.Value.DepartureHeading);

            yield return sector.Teardown();
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Lull_OpensABeaconAwayFromTheHero_AndFlyingInClaimsIt()
        {
            var player = SpawnKinematicShip(Vector2.zero);
            var template = SpawnKinematicShip(new Vector2(500f, 500f));
            template.gameObject.SetActive(false);

            var sectorGO = TrackGO(new GameObject("LullSector"));
            var sector = sectorGO.AddComponent<Sector>();

            var directorGO = new GameObject("WaveDirector");
            directorGO.transform.SetParent(sectorGO.transform, false);
            var director = directorGO.AddComponent<WaveDirector>();
            director.Configure(new[] { new WaveDirector.RosterEntry { template = template } }, new[]
            {
                new SectorPhase { seconds = 1f, interval = 1f, cap = 0 },
                new SectorPhase { seconds = 1f, interval = 1f, cap = 0 },
            }, lullSeconds: 5f);

            var beacon = NewZone(sectorGO.transform, "Beacon");
            var spine = sectorGO.AddComponent<PhaseSpineModule>();
            spine.Bind(director, NewZone(sectorGO.transform, "Zone"), beacon);

            sector.SetManifest(null, new SectorSpawner[] { director }, new SectorModule[] { spine });
            sector.Initialize(_unitService, _objectives, true, default, player);

            yield return sector.Setup();
            yield return WaitSeconds(() => _objectives.SpineTarget == beacon.transform, 5f);
            Assert.AreEqual(beacon.transform, _objectives.SpineTarget, "The lull opens a beacon the marker points at.");
            Assert.IsTrue(beacon.gameObject.activeSelf);
            Assert.AreEqual(BeaconDistance,
                Vector2.Distance(GamePlane.WorldPointToPlane(beacon.transform.position), Vector2.zero), 0.01f,
                "The beacon appears its distance from the player.");
            Assert.IsFalse(spine.BeaconClaimed);

            ShipTestFactory.MoveKinematicShip(player, beacon.transform.position);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.IsTrue(spine.BeaconClaimed, "Flying into the beacon claims it.");
            Assert.IsFalse(beacon.gameObject.activeSelf);
            Assert.IsNull(_objectives.SpineTarget);

            yield return sector.Teardown();
        }
    }
}
#endif

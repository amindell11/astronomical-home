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
    /// <summary>A phased sector end to end: the director's phases run out, the extraction zone appears at its distance from the player, and flying into it ends the sector extracted.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class PhaseSpinePlayModeTests : PlayModeWorldFixture
    {
        private const float ZoneDistance = 150f;

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

        private static IEnumerator WaitSeconds(Func<bool> done, float seconds)
        {
            var deadline = Time.time + seconds;
            while (!done() && Time.time < deadline)
                yield return null;
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator PhasesRunOut_ZoneAppearsAway_AndReachingItExtracts()
        {
            var player = SpawnKinematicShip(Vector2.zero);
            var template = SpawnKinematicShip(new Vector2(500f, 500f));
            template.gameObject.SetActive(false);

            var sectorGO = TrackGO(new GameObject("PhasedSector"));
            var sector = sectorGO.AddComponent<Sector>();

            var directorGO = new GameObject("WaveDirector");
            directorGO.transform.SetParent(sectorGO.transform, false);
            var director = directorGO.AddComponent<WaveDirector>();
            director.Configure(new[] { new WaveDirector.RosterEntry { template = template } },
                new[] { new SectorPhase { seconds = 1f, interval = 1f, cap = 0 } }, lullSeconds: 0f);

            var zoneGO = new GameObject("Zone");
            zoneGO.transform.SetParent(sectorGO.transform, false);
            var zoneCol = zoneGO.AddComponent<SphereCollider>();
            zoneCol.isTrigger = true;
            zoneCol.radius = 4f;
            var zone = zoneGO.AddComponent<ExtractionZone>();

            var spine = sectorGO.AddComponent<PhaseSpineModule>();
            spine.Bind(director, zone);

            sector.SetManifest(null, new SectorSpawner[] { director }, new SectorModule[] { spine });
            sector.Initialize(_unitService, _objectives, true, default, player);
            SectorResult? got = null;
            ((ISector)sector).OnSectorComplete += r => got = r;

            yield return sector.Setup();
            Assert.AreEqual(PhaseSpineModule.StepSurvive, _objectives.SpineStep);
            Assert.IsFalse(zoneGO.activeSelf, "The zone stays hidden while the phases run.");

            yield return WaitSeconds(() => _objectives.SpineStep == PhaseSpineModule.StepExtract, 5f);
            Assert.AreEqual(PhaseSpineModule.StepExtract, _objectives.SpineStep,
                "The spine opens extraction once the phases run out.");
            Assert.IsTrue(zoneGO.activeSelf);
            Assert.AreEqual(ZoneDistance,
                Vector2.Distance(GamePlane.WorldPointToPlane(zone.transform.position), Vector2.zero), 0.01f,
                "The zone appears its distance from the player.");

            ShipTestFactory.MoveKinematicShip(player, zone.transform.position);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return WaitSeconds(() => got.HasValue, 2f);

            Assert.IsTrue(got.HasValue, "Reaching the zone must end the sector.");
            Assert.IsTrue(got.Value.Success, "Reaching the zone ends the sector extracted.");

            yield return sector.Teardown();
        }
    }
}
#endif

#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Ships;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Factions;
using Substrate.Services;
using Substrate.Services.Objectives;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Factions
{
    /// <summary>A sector's faction module raises Entered once, on the hero's first active frame, with the sector's run index and the faction's id; an inactive hero holds it back.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class SectorFactionPlayModeTests : PlayModeWorldFixture
    {
        private UnitService _unitService;
        private ObjectiveService _objectives;
        private Faction _faction;
        private readonly List<GameObject> _created = new();

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            var unitServiceGO = TrackGO(new GameObject("UnitService"));
            _unitService = unitServiceGO.AddComponent<UnitService>();
            _objectives = TrackGO(new GameObject("ObjectiveService")).AddComponent<ObjectiveService>();
            ShipServices.Compose(_unitService, unitServiceGO.transform, presentationEnabled: true);
            _faction = ScriptableObject.CreateInstance<Faction>();
            _faction.id = "veil";
        }

        [TearDown]
        public override void TearDown()
        {
            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            _created.Clear();
            if (_faction != null) { Object.DestroyImmediate(_faction); _faction = null; }
            base.TearDown();
        }

        private GameObject TrackGO(GameObject go) { _created.Add(go); return go; }

        [UnityTest]
        public IEnumerator Entered_RaisesOnceOnTheHerosFirstActiveFrame_WithTheSectorIndexAndFactionId()
        {
            var hero = TrackGO(ShipTestFactory.CreateKinematicPrimitiveShipAt(Vector2.zero).gameObject).GetComponent<Ship>();
            hero.gameObject.SetActive(false);

            var sector = TrackGO(new GameObject("FactionSector")).AddComponent<Sector>();
            var module = sector.gameObject.AddComponent<SectorFaction>();
            module.Bind(_faction);
            sector.SetManifest(null, null, new SectorModule[] { module });
            sector.Initialize(_unitService, _objectives, true, default, hero, 2);

            var raised = new List<(int index, string id)>();
            module.Entered += (index, id) => raised.Add((index, id));
            yield return sector.Setup();
            yield return null;
            yield return null;
            Assert.IsEmpty(raised, "An inactive hero has not entered the sector yet.");

            hero.gameObject.SetActive(true);
            yield return null;
            yield return null;
            CollectionAssert.AreEqual(new[] { (2, "veil") }, raised);

            yield return sector.Teardown();
        }
    }
}
#endif

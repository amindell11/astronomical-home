#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Asteroids.Fields;
using Asteroids.Fields.Core;
using NUnit.Framework;
using Substrate;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Stations;
using Substrate.Services.Objectives;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Sectors
{
    /// <summary>The shipped field and station prefabs built the way a session loads a sector, under an inactive holder: the field's layout seeds no asteroid inside any station's own clear radius, though the dense field packs rocks just outside.</summary>
    [TestFixture]
    [Category("Sectors")]
    [Category("RequiresLfsPng")]
    public class StationClearZonePlayModeTests : PlayModeWorldFixture
    {
        private const string FieldPrefabPath = "Assets/Prefabs/Asteroid/AsteroidController.prefab";
        private const string RepairStationPath = "Assets/Prefabs/Objectives/Repair Station.prefab";
        private const string ModStationPath = "Assets/Prefabs/Objectives/Mod Station.prefab";
        private const float Distance = 60f;
        private const float Spacing = 40f;
        private const float RepairClearRadius = 18f;
        private const float ModClearRadius = 26f;

        private readonly List<Object> _created = new();

        [TearDown]
        public override void TearDown()
        {
            foreach (var item in _created)
                if (item) Object.DestroyImmediate(item);
            _created.Clear();
            base.TearDown();
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator AtSectorStart_NoAsteroidSeedsInsideAStationsClearRadius()
        {
            Random.InitState(1419);
            var holder = Track(new GameObject("SectorLoad"));
            holder.SetActive(false);
            var sector = new GameObject("Sector").AddComponent<Sector>();
            sector.transform.SetParent(holder.transform, false);

            var field = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(FieldPrefabPath), sector.transform)
                .GetComponent<UpdatingAsteroidField>();
            var repairs = NewRepairStations(sector.transform);
            var mods = NewModStations(sector.transform, repairs);
            var spawner = field.GetComponent<AsteroidFieldSpawner>();
            using (var serialized = new SerializedObject(spawner))
            {
                serialized.FindProperty("repairs").objectReferenceValue = repairs;
                serialized.FindProperty("mods").objectReferenceValue = mods;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            sector.SetManifest(null, new SectorSpawner[] { spawner }, new SectorModule[] { repairs, mods }, field);
            sector.Initialize(Track(new GameObject("Units")).AddComponent<UnitService>(),
                Track(new GameObject("Objectives")).AddComponent<ObjectiveService>(), false, default, null);
            yield return sector.Setup();
            holder.SetActive(true);
            yield return null;

            Assert.IsNotNull(field.Model, "The field built its layout on Start.");
            var nearest = float.MaxValue;
            foreach (var station in repairs.Stations)
                nearest = Mathf.Min(nearest, AssertClear(field, station.Position, RepairClearRadius));
            foreach (var station in mods.Stations)
                nearest = Mathf.Min(nearest, AssertClear(field, station.Position, ModClearRadius));
            Assert.Less(nearest, ModClearRadius + 10f, "The dense field packs rocks just outside the clearings.");
        }

        // Asserts no layout asteroid sits inside the circle; returns the nearest one's distance from its center.
        private static float AssertClear(UpdatingAsteroidField field, Vector3 station, float radius)
        {
            var center = GamePlane.WorldPointToPlane(station);
            var origin = GamePlane.WorldPointToPlane(field.transform.position);
            var reach = new Vector2(radius + 20f, radius + 20f);
            var cellMin = field.Model.Layout.CellOf(center - origin - reach);
            var cellMax = field.Model.Layout.CellOf(center - origin + reach);
            var specs = new List<FieldAsteroidSpec>();
            var nearest = float.MaxValue;
            for (var x = cellMin.x; x <= cellMax.x; x++)
            for (var y = cellMin.y; y <= cellMax.y; y++)
            {
                specs.Clear();
                field.Model.GetChunkContents(new Vector2Int(x, y), specs);
                foreach (var spec in specs)
                    nearest = Mathf.Min(nearest, Vector2.Distance(spec.PlanePosition + origin, center));
            }
            Assert.GreaterOrEqual(nearest, radius, $"No asteroid seeds inside the {radius} u clearing at {center}.");
            return nearest;
        }

        private RepairStation NewRepairStations(Transform parent)
        {
            var feel = Track(ScriptableObject.CreateInstance<RepairStationFeelProfile>());
            feel.stationsPerSector = 2;
            var repairs = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RepairStation>(RepairStationPath), parent);
            repairs.Configure(feel, Distance, spacing: Spacing);
            SetClearRadius(repairs, RepairClearRadius);
            return repairs;
        }

        private ModStation NewModStations(Transform parent, RepairStation repairs)
        {
            var feel = Track(ScriptableObject.CreateInstance<ModStationFeelProfile>());
            feel.stationsPerSector = 2;
            var mods = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ModStation>(ModStationPath), parent);
            mods.Configure(feel, Distance, spacing: Spacing, repairs: repairs);
            SetClearRadius(mods, ModClearRadius);
            return mods;
        }

        private static void SetClearRadius(Object module, float radius)
        {
            using var serialized = new SerializedObject(module);
            serialized.FindProperty("clearRadius").floatValue = radius;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private T Track<T>(T item) where T : Object
        {
            _created.Add(item);
            return item;
        }
    }
}
#endif

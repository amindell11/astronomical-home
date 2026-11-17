#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using Substrate.Sectors.Elements.Stations;
using Substrate.Sectors.Elements.Stations.Visual;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Sectors
{
    /// <summary>A placed repair station's model puts its lights out the moment the body's lit parts switch off and coasts its spin, sits at a tilt seeded from where the station was placed, and leaves Unity's random stream untouched.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class StationMotionPlayModeTests
    {
        private const string StationPath = "Assets/Prefabs/Objectives/Repair Station.prefab";

        private readonly MaterialPropertyBlock block = new();
        private GameObject station;
        private FindStationBody body;
        private StationMotion motion;

        [SetUp]
        public void SetUp()
        {
            station = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(StationPath));
            body = station.GetComponentInChildren<FindStationBody>(true);
            motion = station.GetComponentInChildren<StationMotion>(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (station) Object.DestroyImmediate(station);
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator SwitchingLitOff_PutsTheLightsOutAtOnce_WhileThePartsCoast()
        {
            var lamp = Lamp(motion);
            var part = SpinPart(motion);
            yield return null;
            yield return null;
            Assert.AreNotEqual(Vector4.zero, Emission(lamp));

            body.Lit.SetActive(false);
            yield return null;
            Assert.AreEqual(Vector4.zero, Emission(lamp));
            var paper = Paper(lamp);
            var dark = Feel(motion).darkColour;
            Assert.AreEqual(dark.r, paper.r, 1e-4f);
            Assert.AreEqual(dark.g, paper.g, 1e-4f);
            Assert.AreEqual(dark.b, paper.b, 1e-4f);
            Assert.AreEqual(dark.a, paper.a, 1e-4f);
            var coasting = part.localRotation;
            yield return new WaitForSeconds(.5f);
            Assert.Greater(Quaternion.Angle(coasting, part.localRotation), 1f, "The parts coast instead of stopping.");

            body.Lit.SetActive(true);
            yield return null;
            Assert.AreNotEqual(Vector4.zero, Emission(lamp));
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator ATiltIsSeededFromWhereTheStationIsPlaced()
        {
            var first = TiltAt(new Vector3(130f, 40f, 0f));
            var second = TiltAt(new Vector3(-90f, 150f, 0f));
            var third = TiltAt(new Vector3(130f, 40f, 0f));
            Assert.Less(Quaternion.Angle(first, third), 0.01f);
            Assert.Greater(Quaternion.Angle(first, second), 1f);
            yield break;
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator PlacingAStation_LeavesUnityRandomUntouched()
        {
            body.gameObject.SetActive(false);
            Random.InitState(1234);
            body.gameObject.SetActive(true);
            var placed = Random.value;
            Random.InitState(1234);
            Assert.AreEqual(Random.value, placed);
            yield break;
        }

        private Quaternion TiltAt(Vector3 position)
        {
            body.gameObject.SetActive(false);
            body.transform.position = position;
            body.gameObject.SetActive(true);
            return motion.transform.localRotation;
        }

        private Vector4 Emission(Renderer renderer)
        {
            renderer.GetPropertyBlock(block);
            return block.GetVector("_EmissionColor");
        }

        private Color Paper(Renderer renderer)
        {
            renderer.GetPropertyBlock(block);
            return block.GetColor("_PaperColor");
        }

        private static Renderer Lamp(StationMotion motion)
        {
            using var serialized = new SerializedObject(motion);
            return (Renderer)serialized.FindProperty("chase.Array.data[0].lamps.Array.data[0]").objectReferenceValue;
        }

        private static Transform SpinPart(StationMotion motion)
        {
            using var serialized = new SerializedObject(motion);
            return (Transform)serialized.FindProperty("spins.Array.data[0].part").objectReferenceValue;
        }

        private static StationMotionFeelProfile Feel(StationMotion motion)
        {
            using var serialized = new SerializedObject(motion);
            return (StationMotionFeelProfile)serialized.FindProperty("feel").objectReferenceValue;
        }
    }
}
#endif

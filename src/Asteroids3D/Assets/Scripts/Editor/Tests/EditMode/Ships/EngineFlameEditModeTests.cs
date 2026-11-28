using System;
using System.Reflection;
using NUnit.Framework;
using Ships.Visuals;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Ships
{
    [TestFixture]
    [Category("Ships")]
    public sealed class EngineFlameEditModeTests
    {
        private const float Length = 2.5f;

        private GameObject host;
        private EngineFlameFeel feel;
        private Material material;
        private Material emission;

        [TearDown]
        public void TearDown()
        {
            if (host) Object.DestroyImmediate(host);
            if (feel) Object.DestroyImmediate(feel);
            if (material) Object.DestroyImmediate(material);
            if (emission) Object.DestroyImmediate(emission);
        }

        [Test]
        public void StartsAtThePilotLength()
        {
            var flame = NewFlame();
            Assert.That(flame.CurrentLength, Is.EqualTo(feel.PilotLength));
        }

        [Test]
        public void ThrustingForTheIgniteTime_ReachesFull_AndTheSheetIsLengthTall()
        {
            var flame = NewFlame();
            Tick(flame, true, feel.IgniteTime);

            Assert.That(flame.CurrentLength, Is.EqualTo(1f).Within(1e-4f));
            var sheet = flame.transform.GetChild(0);
            Assert.That(sheet.localScale.y, Is.EqualTo(Length).Within(1e-4f));
            Assert.That(sheet.localPosition.y, Is.EqualTo(-Length / 2f).Within(1e-4f));
        }

        [Test]
        public void AfterThrustEnds_CutoffReturnsToThePilot_AndNoLower()
        {
            var flame = NewFlame();
            Tick(flame, true, feel.IgniteTime);
            Assert.That(flame.CurrentLength, Is.EqualTo(1f).Within(1e-4f));

            Tick(flame, false, feel.CutoffTime * (1f - feel.PilotLength));
            Assert.That(flame.CurrentLength, Is.EqualTo(feel.PilotLength).Within(1e-4f));

            Tick(flame, false, feel.CutoffTime);
            Assert.That(flame.CurrentLength, Is.EqualTo(feel.PilotLength).Within(1e-4f));
        }

        [Test]
        public void WidthScale_IsTheSquareRootOfTheLengthScale()
        {
            var flame = NewFlame();
            Tick(flame, true, feel.IgniteTime * 0.5f);

            var sheet = flame.transform.GetChild(0);
            var lengthScale = sheet.localScale.y / Length;
            Assert.That(lengthScale, Is.EqualTo(flame.CurrentLength).Within(1e-4f));
            Assert.That(sheet.localScale.x / (Length * feel.CellAspect), Is.EqualTo(Mathf.Sqrt(lengthScale)).Within(1e-4f));
        }

        [Test]
        public void ColourOf_NormalisesHdrEmission_AndKeepsAnInRangeColour()
        {
            var shader = Shader.Find("Astronomical/Drawn/Surface");
            Assert.That(shader, Is.Not.Null, "a shader with emission");
            emission = new Material(shader);

            emission.SetColor("_EmissionColor", new Color(2f, 1f, 0f, 0.25f));
            var hdr = EngineFlame.ColourOf(emission);
            Assert.That(hdr.r, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(hdr.g, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(hdr.b, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(hdr.a, Is.EqualTo(1f));

            var inside = new Color(0.4f, 0.8f, 0.2f, 0.3f);
            emission.SetColor("_EmissionColor", inside);
            var kept = EngineFlame.ColourOf(emission);
            Assert.That(kept.r, Is.EqualTo(inside.r).Within(1e-5f));
            Assert.That(kept.g, Is.EqualTo(inside.g).Within(1e-5f));
            Assert.That(kept.b, Is.EqualTo(inside.b).Within(1e-5f));
            Assert.That(kept.a, Is.EqualTo(1f));
        }

        [Test]
        public void Awake_WithNoFeel_Throws()
        {
            var flame = Build(null);
            var thrown = Assert.Throws<TargetInvocationException>(() => Wake(flame));
            Assert.That(thrown.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(thrown.InnerException.Message, Does.Contain(host.name));
        }

        private EngineFlame NewFlame()
        {
            var shader = Shader.Find("Astronomical/Vfx/Engine Flame");
            Assert.That(shader, Is.Not.Null, "the engine flame shader");
            material = new Material(shader);
            feel = ScriptableObject.CreateInstance<EngineFlameFeel>();
            var feelObject = new SerializedObject(feel);
            feelObject.FindProperty("material").objectReferenceValue = material;
            feelObject.ApplyModifiedPropertiesWithoutUndo();
            var flame = Build(feel);
            Wake(flame);
            return flame;
        }

        private EngineFlame Build(EngineFlameFeel profile)
        {
            host = new GameObject("Nozzle");
            host.SetActive(false);
            var flame = host.AddComponent<EngineFlame>();
            var so = new SerializedObject(flame);
            so.FindProperty("feel").objectReferenceValue = profile;
            so.FindProperty("length").floatValue = Length;
            so.ApplyModifiedPropertiesWithoutUndo();
            return flame;
        }

        private static void Wake(EngineFlame flame)
        {
            flame.gameObject.SetActive(true);
            // Edit mode never wakes a part, so the test calls Awake itself.
            var awake = typeof(EngineFlame).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            awake.Invoke(flame, null);
        }

        private static void Tick(EngineFlame flame, bool thrusting, float duration)
        {
            const int steps = 8;
            var dt = duration / steps;
            for (var i = 0; i < steps; i++)
                flame.Tick(thrusting, dt);
        }
    }
}

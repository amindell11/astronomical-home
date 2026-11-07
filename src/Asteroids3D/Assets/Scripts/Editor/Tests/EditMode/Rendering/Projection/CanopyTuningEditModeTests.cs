using System;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UI.Projection;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Rendering.Projection
{
    [Category("UI")]
    public sealed class CanopyTuningEditModeTests
    {
        private CanopyProjectionProfile profile;
        private string export;

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<CanopyProjectionProfile>();
            export = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Scripts/Editor/Tests/EditMode/Rendering/Projection/OwnerSettings.json").text;
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(profile);

        [Test]
        public void OwnerExport_PreservesEveryRetainedValue_AndIgnoresCurvatureAndArtwork()
        {
            CanopyTunerSettings.Parse(export).Apply(profile, CanopyState.Base);
            Assert.That(profile.personalityEnabled, Is.True);
            Assert.That(profile.baseSettings, Is.EqualTo(new CanopySettings
            {
                glow = .55f, aberration = 7f, beamJitter = .7f, afterglow = .5f, scanlines = .92f,
                flicker = .8f, warmth = .6f, wear = .73f, glassDistortion = .96f,
                damageShudder = 12.6f, transitionGlitch = .75f
            }));
            var withoutCurvature = JObject.Parse(export);
            ((JObject)withoutCurvature["personality"]).Remove("curvature");
            Assert.That(CanopyTunerSettings.Parse(withoutCurvature.ToString()).Settings,
                Is.EqualTo(profile.baseSettings));
        }

        [Test]
        public void ImportZeroValuesIntoAState_OverridesEveryFieldAndPreservesBlendTimes()
        {
            var root = JObject.Parse(export);
            root["personalityEnabled"] = false;
            foreach (var property in ((JObject)root["personality"]).Properties()) property.Value = 0;
            profile.lowHull.blendInSeconds = .7f;
            profile.lowHull.blendOutSeconds = 1.2f;
            var before = profile.baseSettings;
            CanopyTunerSettings.Parse(root.ToString()).Apply(profile, CanopyState.LowHull);
            Assert.That(profile.lowHull.fields, Is.EqualTo(CanopyFields.All));
            Assert.That(profile.Resolve(CanopyState.LowHull), Is.EqualTo(default(CanopySettings)));
            Assert.That(profile.lowHull.blendInSeconds, Is.EqualTo(.7f));
            Assert.That(profile.lowHull.blendOutSeconds, Is.EqualTo(1.2f));
            Assert.That(profile.baseSettings, Is.EqualTo(before));
            Assert.That(profile.personalityEnabled, Is.False);
        }

        [TestCase("glow")]
        [TestCase("aberration")]
        [TestCase("beamJitter")]
        [TestCase("afterglow")]
        [TestCase("scanlines")]
        [TestCase("flicker")]
        [TestCase("warmth")]
        [TestCase("wear")]
        [TestCase("glassDistortion")]
        [TestCase("damageShudder")]
        [TestCase("transitionGlitch")]
        public void ImportRejectsMissingKeys(string key)
        {
            var root = JObject.Parse(export);
            ((JObject)root["personality"]).Remove(key);
            Assert.Throws<FormatException>(() => CanopyTunerSettings.Parse(root.ToString()));
        }

        [TestCase("{bad")]
        [TestCase("[]")]
        [TestCase("{}")]
        [TestCase("{\"schemaVersion\":2}")]
        [TestCase("{\"schemaVersion\":1,\"personalityEnabled\":0,\"personality\":{}}")]
        [TestCase("{\"schemaVersion\":1,\"personalityEnabled\":true}")]
        public void ImportRejectsMalformedEnvelopes(string json) =>
            Assert.Throws<FormatException>(() => CanopyTunerSettings.Parse(json));

        [TestCase("-0.1")]
        [TestCase("2.01")]
        [TestCase("NaN")]
        [TestCase("Infinity")]
        [TestCase("null")]
        [TestCase("true")]
        [TestCase("\"0.5\"")]
        public void InvalidValueThrowsBeforeApplyingAnything(string token)
        {
            var root = JObject.Parse(export);
            root["personality"]["glow"] = JToken.Parse(token);
            var before = JsonUtility.ToJson(profile);
            Assert.Throws<FormatException>(() => CanopyTunerSettings.Parse(root.ToString()).Apply(profile, CanopyState.Base));
            Assert.That(JsonUtility.ToJson(profile), Is.EqualTo(before));
        }

        [Test]
        public void WinningStateInheritsOnlyFromBase_AndAnExplicitZeroOverridesIt()
        {
            profile.baseSettings.glow = .8f;
            profile.baseSettings.aberration = 6f;
            profile.lowHull.fields = CanopyFields.Glow | CanopyFields.Aberration;
            profile.lowHull.settings.glow = .3f;
            profile.lowHull.settings.aberration = 2f;
            profile.bigMoment.fields = CanopyFields.Glow;
            profile.bigMoment.settings.glow = 0f;
            var selected = CanopyStateBlend.Select(true, true);
            Assert.That(selected, Is.EqualTo(CanopyState.BigMoment));
            Assert.That(profile.Resolve(selected).glow, Is.Zero);
            Assert.That(profile.Resolve(selected).aberration, Is.EqualTo(6f));
            Assert.That(CanopyStateBlend.Select(true, false), Is.EqualTo(CanopyState.LowHull));
            Assert.That(CanopyStateBlend.Select(false, false), Is.EqualTo(CanopyState.Base));
        }

        [Test]
        public void InterruptedBlendStartsFromDisplayedValues_AndExitUsesTheLeavingBlock()
        {
            profile.baseSettings.glow = 0f;
            profile.lowHull.fields = profile.bigMoment.fields = CanopyFields.Glow;
            profile.lowHull.settings.glow = 1f;
            profile.bigMoment.settings.glow = 2f;
            profile.lowHull.blendInSeconds = 1f;
            profile.bigMoment.blendInSeconds = .5f;
            profile.bigMoment.blendOutSeconds = 2f;
            var blend = new CanopyStateBlend(profile);
            blend.Tick(CanopyState.LowHull, .5f);
            Assert.That(blend.Settings.glow, Is.EqualTo(.5f));
            blend.Tick(CanopyState.BigMoment, 0f);
            Assert.That(blend.Settings.glow, Is.EqualTo(.5f));
            blend.Tick(CanopyState.BigMoment, .25f);
            Assert.That(blend.Settings.glow, Is.EqualTo(1.25f));
            blend.Tick(CanopyState.LowHull, 1f);
            Assert.That(blend.Settings.glow, Is.EqualTo(1.125f));
            blend.Tick(CanopyState.LowHull, 1f);
            Assert.That(blend.Settings.glow, Is.EqualTo(1f));
            profile.lowHull.settings.glow = .7f;
            blend.Tick(CanopyState.LowHull, 0f);
            Assert.That(blend.Settings.glow, Is.EqualTo(.7f), "live edits reach a settled state");
        }
    }
}

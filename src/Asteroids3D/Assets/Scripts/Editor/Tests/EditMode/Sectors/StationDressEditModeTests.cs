using System;
using System.Collections.Generic;
using NUnit.Framework;
using Substrate.Sectors;
using Substrate.Sectors.Elements.Stations;
using Substrate.Sectors.Elements.Stations.Visual;
using Substrate.Sectors.Factions;
using Substrate.Sessions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Sectors
{
    /// <summary>A placed point rolls one palette of its faction's dress and the roll reaches every palette, a row fails when its faction cannot dress the stations it places, every trial row passes its layout's row check, and each faction's dressed model matches the plain one's wiring and paints from the palette it wears.</summary>
    [TestFixture]
    [Category("Sectors")]
    public class StationDressEditModeTests
    {
        private const string FeelPath = "Assets/Settings/Game/StationMotionFeel.asset";
        private const string RunPath = "Assets/Settings/Game/SurvivalTrialRun.asset";
        private const string ShoalPath = "Assets/Settings/Factions/Shoal.asset";
        private const string VeilPath = "Assets/Settings/Factions/Veil.asset";
        private const string BastionPath = "Assets/Settings/Factions/Bastion.asset";

        [Test]
        public void ThePaletteRoll_IsTheSameForTheSamePosition()
        {
            var dress = ThreePalettes();
            foreach (var position in Positions())
            {
                var first = dress.PaletteAt(position).name;
                var second = dress.PaletteAt(position).name;
                Assert.AreEqual(first, second, "The same placed point rolls the same palette.");
            }
        }

        [Test]
        public void ThePaletteRoll_ReachesEveryPalette()
        {
            var dress = ThreePalettes();
            var names = new HashSet<string>();
            foreach (var position in Positions())
                names.Add(dress.PaletteAt(position).name);
            CollectionAssert.AreEquivalent(new[] { "a", "b", "c" }, names,
                "The positions reach every palette.");

            var bastion = AssetDatabase.LoadAssetAtPath<Faction>(BastionPath);
            Assert.IsTrue(bastion, BastionPath);
            Assert.AreEqual(2, bastion.stationDress.palettes.Length, "Bastion has two palettes.");
            var reached = new HashSet<string>();
            foreach (var position in Positions())
                reached.Add(bastion.stationDress.PaletteAt(position).name);
            foreach (var palette in bastion.stationDress.palettes)
                Assert.IsTrue(reached.Contains(palette.name), $"The positions reach Bastion's '{palette.name}'.");
        }

        [TestCase("Assets/Prefabs/Objectives/Repair Station.prefab")]
        [TestCase("Assets/Prefabs/Objectives/Mod Station.prefab")]
        [TestCase("Assets/Prefabs/Objectives/Armory.prefab")]
        public void ARowWhoseFactionDressesNoStations_FailsItsRowCheck(string path)
        {
            AssertUndressedRowFails(path, null);
        }

        [TestCase("Assets/Prefabs/Objectives/Repair Station.prefab")]
        [TestCase("Assets/Prefabs/Objectives/Mod Station.prefab")]
        [TestCase("Assets/Prefabs/Objectives/Armory.prefab")]
        public void ARowWhoseFactionLacksTheKindsModel_FailsItsRowCheck(string path)
        {
            AssertUndressedRowFails(path, faction =>
                faction.stationDress.palettes = new[] { new StationPalette { name = "only" } });
        }

        [Test]
        public void EachTrialRow_PassesItsLayoutsRowCheck()
        {
            var run = AssetDatabase.LoadAssetAtPath<SectorRun>(RunPath);
            Assert.IsTrue(run, RunPath);
            foreach (var row in run.Sectors)
                Assert.DoesNotThrow(() => row.prefab.CheckRow(row),
                    row.faction ? row.faction.name : "a row with no faction");
        }

        [Test]
        public void EachFactionsDressedModel_IsWiredLikeThePlainOne()
        {
            var feel = AssetDatabase.LoadAssetAtPath<StationMotionFeelProfile>(FeelPath);
            foreach (var factionPath in new[] { ShoalPath, VeilPath, BastionPath })
            {
                var faction = AssetDatabase.LoadAssetAtPath<Faction>(factionPath);
                Assert.IsTrue(faction, factionPath);
                foreach (StationKind kind in Enum.GetValues(typeof(StationKind)))
                {
                    var where = $"{faction.name} {kind}";
                    var dressed = faction.stationDress.ModelFor(kind);
                    Assert.IsTrue(dressed, where);
                    var plain = PlainModel(kind);
                    Assert.IsTrue(plain, where);
                    Assert.AreNotSame(plain, dressed, where);

                    var motion = dressed.GetComponent<StationMotion>();
                    var plainMotionComponent = plain.GetComponent<StationMotion>();
                    Assert.IsTrue(motion, where);
                    Assert.IsTrue(plainMotionComponent, where);
                    using var motionSerialized = new SerializedObject(motion);
                    Assert.AreSame(feel, motionSerialized.FindProperty("feel").objectReferenceValue, where);
                    using var plainMotion = new SerializedObject(plainMotionComponent);
                    Assert.AreEqual(plainMotion.FindProperty("spins").arraySize,
                        motionSerialized.FindProperty("spins").arraySize, where);
                    Assert.Greater(motionSerialized.FindProperty("steadyLights").arraySize, 0, where);
                    Assert.Greater(motionSerialized.FindProperty("chase").arraySize, 0, where);

                    using var modelSerialized = new SerializedObject(dressed);
                    Assert.Greater(modelSerialized.FindProperty("windows").arraySize, 0, $"{where} windows");
                    var wired = new HashSet<Object>();
                    foreach (var role in new[] { "bone", "canvas", "slate", "ochre", "brick", "blue" })
                    {
                        var renderers = modelSerialized.FindProperty(role);
                        for (var i = 0; i < renderers.arraySize; i++)
                            wired.Add(renderers.GetArrayElementAtIndex(i).objectReferenceValue);
                    }
                    foreach (var renderer in dressed.GetComponentsInChildren<Renderer>(true))
                        if (renderer.sharedMaterial.IsKeywordEnabled("_PAINT_LAYERS"))
                            Assert.IsTrue(wired.Contains(renderer), $"{where}: painted '{renderer.name}' takes a role.");
                }
            }
        }

        [Test]
        public void WearingAPalette_PaintsEveryRoleAndWindow()
        {
            var bastion = AssetDatabase.LoadAssetAtPath<Faction>(BastionPath);
            Assert.IsTrue(bastion, BastionPath);
            var prefab = bastion.stationDress.ModelFor(StationKind.Repair);
            Assert.IsTrue(prefab, "Bastion dresses a repair dock.");
            var windowLight = new Color(1.5f, 0.5f, 0.25f);
            var instance = Object.Instantiate(prefab);
            try
            {
                instance.Wear(new StationPalette { brush = 0.7f, windowLight = windowLight });
                var block = new MaterialPropertyBlock();
                using var serialized = new SerializedObject(instance);
                foreach (var role in new[] { "bone", "canvas", "slate", "ochre", "brick", "blue" })
                {
                    var renderers = serialized.FindProperty(role);
                    Assert.Greater(renderers.arraySize, 0, role);
                    for (var i = 0; i < renderers.arraySize; i++)
                    {
                        var renderer = (Renderer)renderers.GetArrayElementAtIndex(i).objectReferenceValue;
                        renderer.GetPropertyBlock(block);
                        Assert.AreEqual(0.7f, block.GetFloat("_PaintShadowStrength"), 1e-4f, role);
                        Assert.AreEqual(0.7f, block.GetFloat("_PaintLightStrength"), 1e-4f, role);
                    }
                }

                var expected = (Vector4)windowLight.linear;
                var windows = serialized.FindProperty("windows");
                Assert.Greater(windows.arraySize, 0, "windows");
                for (var i = 0; i < windows.arraySize; i++)
                {
                    var renderer = (Renderer)windows.GetArrayElementAtIndex(i).objectReferenceValue;
                    renderer.GetPropertyBlock(block);
                    var emission = block.GetVector("_EmissionColor");
                    Assert.AreEqual(expected.x, emission.x, 1e-4f, "window emission r");
                    Assert.AreEqual(expected.y, emission.y, 1e-4f, "window emission g");
                    Assert.AreEqual(expected.z, emission.z, 1e-4f, "window emission b");
                    Assert.AreEqual(expected.w, emission.w, 1e-4f, "window emission a");
                }
            }
            finally
            {
                Object.DestroyImmediate(instance.gameObject);
            }
        }

        private static void AssertUndressedRowFails(string path, Action<Faction> dress)
        {
            var module = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<FindStation>();
            var reader = (ISectorRowReader)module;
            var faction = ScriptableObject.CreateInstance<Faction>();
            try
            {
                dress?.Invoke(faction);
                Assert.Throws<InvalidOperationException>(() => reader.CheckRow(RowPlacingOne(module, faction)), path);
            }
            finally
            {
                Object.DestroyImmediate(faction);
            }
        }

        private static SectorEntry RowPlacingOne(FindStation module, Faction faction)
        {
            var row = new SectorEntry { faction = faction };
            switch (module.Kind)
            {
                case StationKind.Repair:
                    row.repairStations = 1;
                    break;
                case StationKind.Mod:
                    row.modStations = 1;
                    break;
                case StationKind.Armory:
                    row.armories = 1;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(module), module.Kind, "No station count for this kind.");
            }
            return row;
        }

        private static StationDress ThreePalettes() => new()
        {
            palettes = new[]
            {
                new StationPalette { name = "a" },
                new StationPalette { name = "b" },
                new StationPalette { name = "c" }
            }
        };

        private static Vector3[] Positions()
        {
            var positions = new Vector3[64];
            for (var i = 0; i < positions.Length; i++)
            {
                var radius = Mathf.Lerp(120f, 180f, i / 63f);
                var angle = i * Mathf.PI * 2f / positions.Length;
                positions[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            }
            return positions;
        }

        private static StationModel PlainModel(StationKind kind)
        {
            var path = kind switch
            {
                StationKind.Repair => "Assets/Prefabs/Objectives/Repair Station.prefab",
                StationKind.Mod => "Assets/Prefabs/Objectives/Mod Station.prefab",
                StationKind.Armory => "Assets/Prefabs/Objectives/Armory.prefab",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "A station kind with no prefab.")
            };
            var module = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<FindStation>();
            using var serialized = new SerializedObject(module);
            return ((FindStationBody)serialized.FindProperty("body").objectReferenceValue).Model;
        }
    }
}

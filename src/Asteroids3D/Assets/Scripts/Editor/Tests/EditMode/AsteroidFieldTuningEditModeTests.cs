using System.Reflection;
using Asteroids.Fields;
using Asteroids.Fields.Core;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Contract tests for the field debugging/tuning tools: the settings
    /// version counter that drives live rebuilds, and the guarantee that the
    /// edit-mode noise preview (density-only params) matches what the runtime
    /// layout generates.
    /// </summary>
    [Category("Asteroids")]
    public class AsteroidFieldTuningEditModeTests
    {
        [Test]
        public void SettingsVersion_IncrementsOnValidate()
        {
            var settings = ScriptableObject.CreateInstance<AsteroidFieldSettings>();
            try
            {
                settings.maxAsteroids = 0; // disable the worst-case warning for this test
                var onValidate = typeof(AsteroidFieldSettings)
                    .GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(onValidate);

                var before = settings.Version;
                onValidate.Invoke(settings, null);
                onValidate.Invoke(settings, null);
                Assert.AreEqual(before + 2, settings.Version);
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void SettingsVersion_IsNotSerialized()
        {
            var field = typeof(AsteroidFieldSettings).GetField("Version");
            Assert.IsNotNull(field);
            Assert.IsTrue(field.IsDefined(typeof(System.NonSerializedAttribute), false),
                "Version must stay NonSerialized: OnValidate is editor-only, so builds must not carry stale values");
        }

        [Test]
        public void RebuildField_IsPublic_WithContextMenu()
        {
            var method = typeof(UpdatingAsteroidField).GetMethod("RebuildField", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(method, "live tuning needs a public RebuildField()");
            Assert.IsTrue(method.IsDefined(typeof(ContextMenu), false), "RebuildField should be reachable from the inspector context menu");
        }

        [Test]
        public void DensityPreview_WithoutAttributeParams_MatchesFullLayout()
        {
            // The editor heatmap builds a layout with density params only; it
            // must sample identically to the fully-parameterized runtime layout.
            FieldGenerationParams DensityOnly() => new()
            {
                CellSize = 40f,
                AverageAsteroidsPerCell = 6f,
                NoiseFrequency = 0.35f,
                DensityMultiplierRange = new Vector2(0.3f, 1.7f),
                FieldRadius = 400f,
                MeshVolumes = System.Array.Empty<float>()
            };
            var full = DensityOnly();
            full.MeshVolumes = new[] { 1f, 3f, 8f };
            full.MeshDensity = 2f;
            full.MassScaleRange = new Vector2(0.5f, 2f);
            full.VelocityRange = new Vector2(0.5f, 2f);
            full.SpinRange = new Vector2(-30f, 30f);
            full.AmbientDrift = true;

            var preview = new AsteroidFieldLayout(4242, DensityOnly());
            var runtime = new AsteroidFieldLayout(4242, full);

            for (var cx = -6; cx <= 6; cx += 2)
            for (var cy = -6; cy <= 6; cy += 2)
            {
                Assert.AreEqual(runtime.DensityMultiplier(cx, cy), preview.DensityMultiplier(cx, cy),
                    $"multiplier diverged at ({cx},{cy})");
                Assert.AreEqual(runtime.CountForCell(cx, cy), preview.CountForCell(cx, cy),
                    $"count diverged at ({cx},{cy})");
            }
        }
    }
}

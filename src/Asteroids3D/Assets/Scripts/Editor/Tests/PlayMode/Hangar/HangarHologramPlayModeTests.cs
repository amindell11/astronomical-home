#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using Game;
using NUnit.Framework;
using Ships;
using Ships.Presentation;
using Ships.Visuals;
using UI.Hangar;
using UI.Hangar.Hardware;
using UI.Hangar.Hologram;
using UI.Projection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Hangar
{
    [Category("UI"), Category("RequiresGraphics")]
    public sealed class HangarHologramPlayModeTests
    {
        private const string ProfilePath = "Assets/Settings/UI/HangarHologram.asset";
        private const string GameHostPath = "Assets/Prefabs/Game/GameHost.prefab";
        private const string ProjectorPath = "Assets/Prefabs/UI/HangarProjector.prefab";
        private const string ConsolePath = "Assets/Prefabs/UI/HangarConsole.prefab";
        private const string ValisPath = "Assets/Prefabs/Ships/Valis.prefab";
        private const float EntrySeconds = 1.8f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        private HangarHologramProfile profile;
        private HangarHologramProfile runtimeProfile;
        private HangarPreviewStage stage;
        private GameObject instance;

        [SetUp]
        public void SetUp() => profile = AssetDatabase.LoadAssetAtPath<HangarHologramProfile>(ProfilePath);

        [TearDown]
        public void TearDown()
        {
            if (stage) Object.Destroy(stage.gameObject);
            if (instance) Object.Destroy(instance);
            if (runtimeProfile) Object.Destroy(runtimeProfile);
        }

        [UnityTest]
        public IEnumerator EveryOfferedShip_WearsTheHologram_AcrossSwitchesAndPoolReuse()
        {
            var ships = AssetDatabase.LoadAssetAtPath<GameHost>(GameHostPath).hangarOffer.ships;
            stage = HangarPreviewStage.Create(null, new Vector2Int(256, 256), new Rect(0f, 0f, 1f, 1f), ships,
                profile.Restyle);
            stage.Show(ships[0], backwards: false);
            yield return Settle(ships[0]);

            // A full lap each way: every ship is hidden, then shown again from the pool.
            var index = 0;
            foreach (var step in Enumerable.Repeat(1, ships.Length).Concat(Enumerable.Repeat(-1, ships.Length)))
            {
                index = (index + step + ships.Length) % ships.Length;
                stage.Show(ships[index], backwards: step < 0);
                yield return Settle(ships[index]);
            }

            var copies = stage.GetComponentsInChildren<HangarPreviewFlight>(true);
            Assert.That(copies.Select(copy => copy.name), Is.EquivalentTo(ships.Select(ship => ship.name + "_Preview")));
            var wide = new MaterialPropertyBlock();
            var slot = new MaterialPropertyBlock();
            var carried = 0;
            foreach (var copy in copies)
            {
                var template = ships.Single(ship => copy.name == ship.name + "_Preview").VisualRig.Silhouette;
                Assert.That(copy.GetComponentsInChildren<HullVisuals>(true), Is.Not.Empty, $"{copy.name} keeps its HullVisuals");
                var silhouette = copy.GetComponent<ShipVisualRig>().Silhouette;
                for (var s = 0; s < silhouette.Count; s++)
                {
                    var r = silhouette[s];
                    var own = template[s].sharedMaterials;
                    var worn = r.sharedMaterials;
                    r.GetPropertyBlock(wide);
                    for (var i = 0; i < worn.Length; i++)
                    {
                        var where = $"{copy.name}/{r.name} slot {i}";
                        var counterpart = profile.Restyle.CounterpartOf(own[i]);
                        Assert.That(worn[i], Is.SameAs(counterpart ? counterpart : own[i]), where);
                        if (!counterpart) continue;
                        r.GetPropertyBlock(slot, i);
                        carried += AssertCarried(own[i], counterpart.shader, slot, where);
                        if (wide.HasColor(BaseColorId) && slot.HasColor(BaseColorId))
                            AssertColor(wide.GetColor(BaseColorId), slot.GetColor(BaseColorId), $"{where} renderer-wide _BaseColor");
                    }
                }
            }
            Assert.That(carried, Is.GreaterThan(0), "some ship values reach the hologram");
        }

        [UnityTest]
        public IEnumerator Surface_TakesTheProfileOpacity_Live()
        {
            var projector = Object.Instantiate(AssetDatabase.LoadAssetAtPath<HangarHologramProjector>(ProjectorPath));
            instance = projector.gameObject;
            runtimeProfile = Object.Instantiate(profile);
            var wiring = new SerializedObject(projector);
            wiring.FindProperty("profile").objectReferenceValue = runtimeProfile;
            wiring.ApplyModifiedPropertiesWithoutUndo();
            var surface = (Renderer)wiring.FindProperty("surface").objectReferenceValue;
            var valis = AssetDatabase.LoadAssetAtPath<Ship>(ValisPath);
            projector.Open(new[] { valis }, valis);

            using var knobs = new SerializedObject(runtimeProfile);
            var block = new MaterialPropertyBlock();
            foreach (var opacity in new[] { .3f, .9f })
            {
                knobs.FindProperty("layerOpacity").floatValue = opacity;
                knobs.ApplyModifiedPropertiesWithoutUndo();
                yield return null;
                surface.GetPropertyBlock(block);
                Assert.That(block.GetFloat(OpacityId), Is.EqualTo(opacity).Within(1e-4f));
            }
        }

        [UnityTest]
        public IEnumerator Cone_SitsAtTheLens_UnderTheSettledFrame()
        {
            var console = Object.Instantiate(AssetDatabase.LoadAssetAtPath<HangarConsole>(ConsolePath));
            instance = console.gameObject;
            console.transform.position = new Vector3(0f, -2000f, 0f);
            var valis = AssetDatabase.LoadAssetAtPath<Ship>(ValisPath);
            var projector = console.Projector;
            using var wiring = new SerializedObject(projector);
            wiring.FindProperty("profile").objectReferenceValue = profile;
            wiring.ApplyModifiedPropertiesWithoutUndo();
            projector.Open(new[] { valis }, valis);
            yield return new WaitForSeconds(EntrySeconds);

            var surface = (Renderer)wiring.FindProperty("surface").objectReferenceValue;
            var cone = projector.GetComponentsInChildren<Renderer>(true)
                .Where(r => r != surface && !r.GetComponentInParent<HangarPreviewFlight>(true)).ToArray();
            Assert.That(cone, Is.Not.Empty, "the projector places a cone");
            var coneAnchor = (Transform)wiring.FindProperty("coneAnchor").objectReferenceValue;
            Assert.That(coneAnchor.childCount, Is.EqualTo(1), "one cone sits under the anchor");
            var root = coneAnchor.GetChild(0);
            Assert.That(cone.Select(r => r.transform.IsChildOf(root)), Is.All.True, "every cone renderer is the cone's");
            Assert.That(Vector3.Distance(root.position, console.ProjectorLens.transform.position), Is.LessThan(.01f));

            var side = wiring.FindProperty("surfaceSize").vector2Value.x * projector.Frame.width;
            Assert.That(Vector3.Distance(root.localScale,
                    profile.ProjectorCone.transform.localScale * (side / HangarPreviewStage.FrameSideInRadii)),
                Is.LessThan(1e-4f), "the cone is authored in framing radii");

            var beam = cone[0].bounds;
            foreach (var r in cone) beam.Encapsulate(r.bounds);
            var frame = projector.Frame;
            var frameTop = surface.transform.TransformPoint(new Vector3(frame.center.x - .5f, frame.yMax - .5f, 0f));
            var camera = console.HardwareCamera;
            Assert.That(camera.WorldToScreenPoint(new Vector3(beam.center.x, beam.max.y, beam.center.z)).y,
                Is.LessThan(camera.WorldToScreenPoint(frameTop).y), "the cone's top sits below the frame's top");
        }

        [Test]
        public void HardwareInFront_DrawsOverTheSurface()
        {
            var console = AssetDatabase.LoadAssetAtPath<HangarConsole>(ConsolePath);
            using var wiring = new SerializedObject(console.Projector);
            var surface = (Renderer)wiring.FindProperty("surface").objectReferenceValue;
            var hologram = surface.sharedMaterial;
            Assert.That(ShaderSource(hologram.shader), Does.Contain("ZTest LEqual"));

            var hardware = console.GetComponentsInChildren<Renderer>(true)
                .Where(r => !r.transform.IsChildOf(console.Projector.transform))
                .SelectMany(r => r.sharedMaterials).Distinct()
                // Glass writes no depth, so it never hides the hologram.
                .Where(material => !ShaderSource(material.shader).Contains("ZWrite Off")).ToArray();
            Assert.That(hardware, Is.Not.Empty, "the console has depth-writing hardware");
            foreach (var material in hardware)
                Assert.That(hologram.renderQueue, Is.GreaterThan(material.renderQueue), material.name);
            Assert.That(console.HardwareCamera.cullingMask & (1 << surface.gameObject.layer), Is.Not.Zero,
                "the console camera draws the surface");
        }

        [UnityTest]
        public IEnumerator Surface_ShowsTheCanopyImage_ThroughAShipStep()
        {
            var console = Object.Instantiate(AssetDatabase.LoadAssetAtPath<HangarConsole>(ConsolePath));
            instance = console.gameObject;
            console.transform.position = new Vector3(0f, -2000f, 0f);
            var ships = AssetDatabase.LoadAssetAtPath<GameHost>(GameHostPath).hangarOffer.ships;
            var projector = console.Projector;
            projector.Open(ships, ships[0]);
            yield return new WaitForSeconds(EntrySeconds);
            AssertSurfaceShowsImage(projector);
            Assert.That(PeakAlpha(CanopyImageOf(projector).Output), Is.GreaterThan(.5f), "the ship");

            projector.Select(ships[1], 1);
            yield return new WaitForSeconds(EntrySeconds);
            AssertSurfaceShowsImage(projector);
        }

        private static void AssertSurfaceShowsImage(HangarHologramProjector projector)
        {
            using var wiring = new SerializedObject(projector);
            var image = (CanopyImage)wiring.FindProperty("image").objectReferenceValue;
            var surface = (Renderer)wiring.FindProperty("surface").objectReferenceValue;
            var block = new MaterialPropertyBlock();
            surface.GetPropertyBlock(block);
            var shown = block.GetTexture(MainTexId);
            Assert.That(shown, Is.SameAs(image.Output));
            Assert.That(shown, Is.Not.SameAs(projector.Stage.Texture));
            Assert.That(image.Output.width, Is.EqualTo(projector.Stage.Texture.width));
            Assert.That(image.Output.height, Is.EqualTo(projector.Stage.Texture.height));
        }

        private static CanopyImage CanopyImageOf(HangarHologramProjector projector)
        {
            using var wiring = new SerializedObject(projector);
            return (CanopyImage)wiring.FindProperty("image").objectReferenceValue;
        }

        private static float PeakAlpha(RenderTexture texture)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var read = new Texture2D(texture.width, texture.height, TextureFormat.RGBAFloat, false, true);
            try
            {
                read.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                read.Apply();
                var peak = 0f;
                foreach (var pixel in read.GetPixels())
                    peak = Mathf.Max(peak, pixel.a);
                return peak;
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(read);
            }
        }

        private IEnumerator Settle(Ship ship)
        {
            for (var deadline = Time.time + 3f; Time.time < deadline;)
            {
                yield return null;
                var active = stage.GetComponentsInChildren<HangarPreviewFlight>();
                if (active.Length == 1 && active[0].name == ship.name + "_Preview") yield break;
            }
            Assert.Fail($"{ship.name}'s handoff never settled");
        }

        private static int AssertCarried(Material own, Shader counterpart, MaterialPropertyBlock block, string where)
        {
            var carried = 0;
            var tagged = Enumerable.Range(0, counterpart.GetPropertyCount())
                .Where(i => (counterpart.GetPropertyFlags(i) & ShaderPropertyFlags.PerRendererData) != 0).ToArray();
            foreach (var i in tagged)
            {
                var id = counterpart.GetPropertyNameId(i);
                if (!own.HasProperty(id)) continue;
                var property = $"{where} {counterpart.GetPropertyName(i)}";
                switch (counterpart.GetPropertyType(i))
                {
                    case ShaderPropertyType.Color:
                        AssertColor(block.GetColor(id), own.GetColor(id), property);
                        break;
                    case ShaderPropertyType.Vector:
                        Assert.That(block.GetVector(id), Is.EqualTo(own.GetVector(id)), property);
                        break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        Assert.That(block.GetFloat(id), Is.EqualTo(own.GetFloat(id)), property);
                        break;
                    case ShaderPropertyType.Int:
                        Assert.That(block.GetInteger(id), Is.EqualTo(own.GetInteger(id)), property);
                        break;
                    case ShaderPropertyType.Texture:
                        var texture = own.GetTexture(id);
                        if (!texture)
                        {
                            Assert.IsFalse(block.HasTexture(id), $"{property} keeps the counterpart's own texture");
                            continue;
                        }
                        Assert.That(block.GetTexture(id), Is.SameAs(texture), property);
                        if ((counterpart.GetPropertyFlags(i) & ShaderPropertyFlags.NoScaleOffset) == 0)
                        {
                            var scale = own.GetTextureScale(id);
                            var offset = own.GetTextureOffset(id);
                            Assert.That(block.GetVector(counterpart.GetPropertyName(i) + "_ST"),
                                Is.EqualTo(new Vector4(scale.x, scale.y, offset.x, offset.y)), property + "_ST");
                        }
                        break;
                }
                carried++;
            }
            return carried;
        }

        // A block's color round-trips through linear space.
        private static void AssertColor(Color actual, Color expected, string what) =>
            Assert.That(Vector4.Distance(actual, expected), Is.LessThan(1e-4f), $"{what}: {actual} vs {expected}");

        private static string ShaderSource(Shader shader) => File.ReadAllText(AssetDatabase.GetAssetPath(shader));
    }
}
#endif

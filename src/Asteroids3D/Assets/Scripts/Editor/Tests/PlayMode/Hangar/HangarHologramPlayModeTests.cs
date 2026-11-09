#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using Ships.Presentation;
using Ships.Visuals;
using UI.Hangar;
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
        private const string ValisPath = "Assets/Prefabs/Ships/Valis.prefab";
        private const float EntrySeconds = 1.8f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ContourColorId = Shader.PropertyToID("_ContourColor");

        private HangarHologramProfile profile;
        private HangarHologramProfile runtimeProfile;
        private HangarPreviewStage stage;
        private GameObject screenRoot;
        private ItemSubset offer;
        private GameObject holder;
        private readonly List<Material> paints = new();

        [SetUp]
        public void SetUp() => profile = AssetDatabase.LoadAssetAtPath<HangarHologramProfile>(ProfilePath);

        [TearDown]
        public void TearDown()
        {
            if (stage) Object.Destroy(stage.gameObject);
            if (screenRoot) Object.Destroy(screenRoot);
            if (offer) Object.Destroy(offer);
            if (runtimeProfile) Object.Destroy(runtimeProfile);
            if (holder) Object.Destroy(holder);
            foreach (var paint in paints) Object.Destroy(paint);
            paints.Clear();
        }

        [UnityTest]
        public IEnumerator EveryOfferedShip_WearsTheHologram_AcrossSwitchesAndPoolReuse()
        {
            var offered = AssetDatabase.LoadAssetAtPath<GameHost>(GameHostPath).hangarOffer.ships;
            // Shipped hulls paint through layer masks and leave _BaseMap empty, so nothing of theirs is carried.
            var ships = offered.Append(PaintedVariant(offered[0])).ToArray();
            stage = HangarPreviewStage.Create(null, new Vector2Int(256, 256), new Rect(0f, 0f, 1f, 1f), ships, profile);
            stage.Show(new ShipLoadout(ships[0], null, null), backwards: false);
            yield return Settle(ships[0]);

            // A full lap each way: every ship is hidden, then shown again from the pool.
            var index = 0;
            foreach (var step in Enumerable.Repeat(1, ships.Length).Concat(Enumerable.Repeat(-1, ships.Length)))
            {
                index = (index + step + ships.Length) % ships.Length;
                stage.Show(new ShipLoadout(ships[index], null, null), backwards: step < 0);
                yield return Settle(ships[index]);
            }

            var copies = stage.GetComponentsInChildren<HangarPreviewFlight>(true);
            Assert.That(copies.Select(copy => copy.name), Is.EquivalentTo(ships.Select(ship => ship.name + "_Preview")));
            var block = new MaterialPropertyBlock();
            var carried = 0;
            foreach (var copy in copies)
            {
                var template = ships.Single(ship => copy.name == ship.name + "_Preview").VisualRig.transform;
                Assert.That(copy.GetComponentsInChildren<HullVisuals>(true), Is.Empty, $"{copy.name} keeps no HullVisuals");
                foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
                {
                    var path = PathBelow(copy.transform, r.transform);
                    var shipRenderer = (path.Length == 0 ? template : template.Find(path)).GetComponent<Renderer>();
                    var where = $"{copy.name}/{path}";
                    r.GetPropertyBlock(block);
                    Assert.IsFalse(block.HasProperty(BaseColorId) || block.HasProperty(ContourColorId),
                        $"{where} holds no renderer-wide ship colors");
                    var worn = r.sharedMaterials;
                    var own = shipRenderer.sharedMaterials;
                    for (var i = 0; i < worn.Length; i++)
                    {
                        var counterpart = own[i] ? profile.CounterpartOf(own[i].shader) : null;
                        Assert.That(worn[i], Is.SameAs(counterpart ? counterpart : own[i]), $"{where} slot {i}");
                        if (!counterpart) continue;
                        r.GetPropertyBlock(block, i);
                        carried += AssertCarried(own[i], counterpart.shader, block, $"{where} slot {i}");
                    }
                }
            }
            Assert.That(carried, Is.GreaterThan(0), "some ship paint reaches the hologram");
        }

        [UnityTest]
        public IEnumerator PreviewImage_TakesTheLayerOpacity_Live()
        {
            var valis = AssetDatabase.LoadAssetAtPath<Ship>(ValisPath);
            screenRoot = new GameObject("ScreenRoot");
            var screen = Object.Instantiate(AssetDatabase.LoadAssetAtPath<HangarScreen>("Assets/Prefabs/UI/HangarScreen.prefab"),
                screenRoot.transform);
            runtimeProfile = Object.Instantiate(profile);
            screen.hologram = runtimeProfile;
            offer = ScriptableObject.CreateInstance<ItemSubset>();
            offer.starter = valis;
            offer.ships = new[] { valis };
            screen.Show(offer, new ShipLoadout(valis, null, null), _ => true, null);
            var authored = screen.previewImage.color;

            using var knobs = new SerializedObject(runtimeProfile);
            foreach (var opacity in new[] { .3f, .9f })
            {
                knobs.FindProperty("layerOpacity").floatValue = opacity;
                knobs.ApplyModifiedPropertiesWithoutUndo();
                yield return null;
                Assert.That(screen.previewImage.color, Is.EqualTo(new Color(authored.r, authored.g, authored.b, opacity)));
            }
        }

        [UnityTest]
        public IEnumerator ProjectorCone_SitsUnderTheSettledShip_OnThePreviewLayer()
        {
            var valis = AssetDatabase.LoadAssetAtPath<Ship>(ValisPath);
            stage = HangarPreviewStage.Create(null, new Vector2Int(256, 256), new Rect(0f, 0f, 1f, 1f), new[] { valis }, profile);
            stage.Show(new ShipLoadout(valis, null, null), backwards: false);
            yield return new WaitForSeconds(EntrySeconds);

            var cone = stage.GetComponentsInChildren<Renderer>(true)
                .Where(r => !r.GetComponentInParent<HangarPreviewFlight>(true)).ToArray();
            Assert.That(cone, Is.Not.Empty, "the stage places a projector cone");
            var layer = LayerMask.NameToLayer("ShipPreview");
            Assert.That(cone.Select(r => r.gameObject.layer), Is.All.EqualTo(layer));
            var beam = Encapsulate(cone);
            var hull = Encapsulate(stage.GetComponentInChildren<ShipVisualRig>().Silhouette);
            Assert.That(beam.max.y, Is.LessThan(hull.center.y), "the cone's top sits below the hull's middle");
            Assert.That(beam.min.y, Is.LessThan(hull.min.y), "the cone's lens sits below the hull");
            Assert.That(beam.center.x, Is.InRange(hull.min.x, hull.max.x));
            Assert.That(beam.center.z, Is.InRange(hull.min.z, hull.max.z));
        }

        private Ship PaintedVariant(Ship ship)
        {
            // Inactive parent: the variant's ship components never wake.
            holder = new GameObject("InactiveShips");
            holder.SetActive(false);
            var variant = Object.Instantiate(ship, holder.transform);
            foreach (var r in variant.VisualRig.GetComponentsInChildren<Renderer>(true))
            {
                var materials = r.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    if (!materials[i] || !profile.CounterpartOf(materials[i].shader) || !materials[i].HasProperty("_BaseMap"))
                        continue;
                    var paint = new Material(materials[i]);
                    paint.SetTexture("_BaseMap", Texture2D.grayTexture);
                    paint.SetTextureScale("_BaseMap", new Vector2(2f, 3f));
                    paint.SetTextureOffset("_BaseMap", new Vector2(.25f, .5f));
                    paints.Add(paint);
                    materials[i] = paint;
                }
                r.sharedMaterials = materials;
            }
            return variant;
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
                        Assert.That(block.GetColor(id), Is.EqualTo(own.GetColor(id)), property);
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
            var names = tagged.Select(counterpart.GetPropertyNameId).ToArray();
            if (!names.Contains(BaseColorId)) Assert.IsFalse(block.HasProperty(BaseColorId), $"{where} holds no _BaseColor");
            if (!names.Contains(ContourColorId)) Assert.IsFalse(block.HasProperty(ContourColorId), $"{where} holds no _ContourColor");
            return carried;
        }

        private static string PathBelow(Transform root, Transform node)
        {
            var path = "";
            for (var t = node; t != root; t = t.parent)
                path = path.Length == 0 ? t.name : t.name + "/" + path;
            return path;
        }

        private static Bounds Encapsulate(IEnumerable<Renderer> renderers)
        {
            var all = renderers.ToArray();
            var bounds = all[0].bounds;
            foreach (var r in all) bounds.Encapsulate(r.bounds);
            return bounds;
        }
    }
}
#endif

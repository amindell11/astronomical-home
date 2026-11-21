using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UI.Hangar.Displays;
using UI.Hangar.Hologram;
using UI.Hangar.Selection;
using UI.Projection;
using UI.Screens;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace UI.Hangar.Hardware
{
    public static class HangarConsoleBuilder
    {
        public const string VisualsPath = "Assets/Visuals/Hangar/Console";
        public const string PrefabPath = "Assets/Prefabs/UI/HangarConsole.prefab";
        private const string ProjectorPath = "Assets/Prefabs/UI/HangarProjector.prefab";
        private const float PixelsPerUnit = 200f;

        [MenuItem("Tools/Hangar/Rebuild console hardware")]
        public static void Rebuild()
        {
            ShipSilhouettes.BakeHangar();
            var manifestPath = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../../art/environments/hangar-console/console-export.json"));
            var manifest = JsonUtility.FromJson<ExportManifest>(File.ReadAllText(manifestPath));
            var authored = manifest.camera;
            foreach (var filename in new[] { "ConsolePaint", "ConsoleEmission", "WindowEtching" })
            {
                var path = $"{VisualsPath}/{filename}.png";
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = filename != "WindowEtching";
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.mipmapEnabled = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.anisoLevel = 4;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = filename == "ConsolePaint" ? 8192 : 4096;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            var modelPath = VisualsPath + "/Console.fbx";
            AssetDatabase.ImportAsset(modelPath);
            var modelImporter = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            modelImporter.materialImportMode = ModelImporterMaterialImportMode.None;
            modelImporter.importCameras = false;
            modelImporter.importLights = false;
            modelImporter.importAnimation = false;
            modelImporter.isReadable = true;
            modelImporter.SaveAndReimport();

            var root = new GameObject("HangarConsole");
            root.SetActive(false);
            try
            {
                var console = root.AddComponent<HangarConsole>();
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.transform.SetParent(root.transform, false);
                model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                var glassPath = VisualsPath + "/ViewportGlass.fbx";
                AssetDatabase.ImportAsset(glassPath);
                var glass = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(glassPath));
                glass.transform.SetParent(root.transform, false);
                glass.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                var windowMaterial = ShaderMaterial("WindowEtching", "Hangar/WindowEtching");
                windowMaterial.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(VisualsPath + "/WindowEtching.png"));
                glass.GetComponentInChildren<MeshRenderer>().sharedMaterial = windowMaterial;
                var paint = Material("ConsolePaint", "ConsolePaint", "ConsoleEmission");
                paint.SetColor("_Starlight", new Color(.18f, .20f, .24f).gamma);
                var main = Glass("MainScreenGlass");
                var detail = Glass("DetailScreenGlass");
                var primary = Glass("PrimaryScreenGlass");
                var ordnance = Glass("OrdnanceScreenGlass");
                var parts = model.GetComponentsInChildren<MeshRenderer>().ToDictionary(renderer => renderer.name);
                var glassMaterials = new Dictionary<string, Material>
                {
                    { "MainScreenGlass", main },
                    { "DetailScreenGlass", detail },
                    { "PrimaryScreenGlass", primary },
                    { "OrdnanceScreenGlass", ordnance },
                };
                foreach (var renderer in parts.Values)
                {
                    renderer.sharedMaterial = glassMaterials.TryGetValue(renderer.name, out var glassMaterial)
                        ? glassMaterial : paint;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                var launchMaterial = ShaderMaterial("LaunchButton", "Hangar/ConsolePaint");
                launchMaterial.SetTexture("_BaseMap", paint.GetTexture("_BaseMap"));
                launchMaterial.SetTexture("_EmissionMap", paint.GetTexture("_EmissionMap"));
                launchMaterial.SetFloat("_LightResponse", 0f);
                EditorUtility.SetDirty(launchMaterial);
                parts["LaunchButton"].sharedMaterial = launchMaterial;
                var legend = ShaderMaterial("LaunchLegend", "Hangar/ConsolePaint");
                legend.SetTexture("_EmissionMap", null);
                legend.SetColor("_Starlight", Color.black);
                legend.SetFloat("_LightResponse", 0f);
                legend.SetColor("_ConstantEmission", new Color(.82f, .90f, 1f));
                foreach (var control in new[]
                         {
                             "Launch", "Previous", "Next", "PrimaryPrevious", "PrimaryNext", "OrdnancePrevious",
                             "OrdnanceNext"
                         })
                {
                    parts[control + "Legend"].sharedMaterial = legend;
                    parts[control + "Legend"].transform.SetParent(parts[control + "Button"].transform, true);
                }

                var cameraObject = new GameObject("HardwareCamera");
                cameraObject.transform.SetParent(root.transform, false);
                cameraObject.transform.localPosition = ToUnity(authored.position);
                cameraObject.transform.localRotation = Quaternion.LookRotation(ToUnity(authored.forward), ToUnity(authored.up));
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.cullingMask = 1 << 5;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 60f;
                camera.fieldOfView = authored.verticalFieldOfView;
                camera.allowHDR = false;
                camera.allowMSAA = true;
                camera.useOcclusionCulling = false;
                camera.enabled = true;
                var composite = new GameObject("Composite", typeof(RectTransform), typeof(Canvas));
                composite.transform.SetParent(root.transform, false);
                var canvas = composite.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = HangarConsole.CompositeSortingOrder;
                var imageObject = new GameObject("CompositeImage", typeof(RectTransform), typeof(RawImage));
                imageObject.transform.SetParent(composite.transform, false);
                var image = imageObject.GetComponent<RawImage>();
                var rect = image.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                image.raycastTarget = false;
                image.material = ShaderMaterial("ConsoleComposite", "Hangar/ConsoleComposite");

                var lens = parts["ProjectorLens"].transform;
                var projectorObject = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(ProjectorPath));
                projectorObject.transform.SetParent(root.transform, false);
                projectorObject.transform.position = lens.position;
                projectorObject.transform.rotation = camera.transform.rotation;

                var feature = CanopyFeature();
                var mainView = HangarDisplayBuilder.BuildMain(root.transform, new Vector3(0f, -300f, 0f),
                    Resolution(manifest, "MainScreenGlass"), Content(manifest, "MainScreenGlass"), feature);
                var detailView = HangarDisplayBuilder.BuildDetail(root.transform, new Vector3(200f, -300f, 0f),
                    Resolution(manifest, "DetailScreenGlass"), Content(manifest, "DetailScreenGlass"), feature);
                var primaryView = HangarDisplayBuilder.BuildWeapon(root.transform, "PrimaryDisplay",
                    new Vector3(400f, -300f, 0f), Resolution(manifest, "PrimaryScreenGlass"),
                    Content(manifest, "PrimaryScreenGlass"), feature, HangarSlot.Weapon, "PRIMARY");
                var ordnanceView = HangarDisplayBuilder.BuildWeapon(root.transform, "OrdnanceDisplay",
                    new Vector3(600f, -300f, 0f), Resolution(manifest, "OrdnanceScreenGlass"),
                    Content(manifest, "OrdnanceScreenGlass"), feature, HangarSlot.Ordnance, "ORDNANCE");
                PlaceLight(mainView.GetComponent<HangarDisplay>(), 1.4f, 8f);
                PlaceLight(detailView.GetComponent<HangarDisplay>(), .5f, 5f);
                PlaceLight(primaryView.GetComponent<HangarDisplay>(), .5f, 4f);
                PlaceLight(ordnanceView.GetComponent<HangarDisplay>(), .5f, 4f);
                var knobAxis = AuthoredUp(parts["SelectorKnob"]);
                var knob = Control(parts["SelectorKnob"], knobAxis, .2f, camera);
                var turn = parts["SelectorKnob"].gameObject.AddComponent<HangarKnob>();
                var previous = Control(parts["PreviousButton"], AuthoredUp(parts["PreviousButton"]), .28f, camera);
                var next = Control(parts["NextButton"], AuthoredUp(parts["NextButton"]), .28f, camera);
                var launch = Control(parts["LaunchButton"], AuthoredUp(parts["LaunchButton"]), .35f, camera);
                var primaryPrevious = Control(parts["PrimaryPreviousButton"], AuthoredUp(parts["PrimaryPreviousButton"]), .28f, camera);
                var primaryNext = Control(parts["PrimaryNextButton"], AuthoredUp(parts["PrimaryNextButton"]), .28f, camera);
                var ordnancePrevious = Control(parts["OrdnancePreviousButton"], AuthoredUp(parts["OrdnancePreviousButton"]), .28f, camera);
                var ordnanceNext = Control(parts["OrdnanceNextButton"], AuthoredUp(parts["OrdnanceNextButton"]), .28f, camera);
                root.AddComponent<HangarConsoleRaycaster>();

                var glows = new GameObject("Glows");
                glows.transform.SetParent(root.transform, false);
                var glowMaterial = ShaderMaterial("ConsoleGlow", "Hangar/ConsoleGlow");
                var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                var lampGlows = GlowCards(glows.transform, manifest.lamps, quad, glowMaterial, camera);
                if (manifest.downlights == null || manifest.downlights.Length > 2)
                    throw new InvalidOperationException(
                        "console-export.json lists " +
                        (manifest.downlights == null ? "no downlights" : manifest.downlights.Length + " downlights") +
                        "; the console lights two overheads.");
                var overheadGlows = GlowCards(glows.transform, manifest.downlights, quad, glowMaterial, camera);
                var lensGlow = GlowCard(glows.transform, "ProjectorLens glow", lens.position,
                    new Color(.45f, .9f, 1f), Vector2.zero, quad, glowMaterial, camera, .06f);

                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = 5;

                var data = new SerializedObject(console);
                data.FindProperty("hardwareCamera").objectReferenceValue = camera;
                data.FindProperty("referenceVerticalFov").floatValue = authored.verticalFieldOfView;
                foreach (var name in new[]
                         {
                             "MainScreenGlass", "DetailScreenGlass", "PrimaryScreenGlass", "OrdnanceScreenGlass",
                             "ProjectorLens"
                         })
                    data.FindProperty(char.ToLowerInvariant(name[0]) + name.Substring(1)).objectReferenceValue = parts[name];
                data.FindProperty("selectorKnob").objectReferenceValue = knob;
                data.FindProperty("knob").objectReferenceValue = turn;
                data.FindProperty("previousButton").objectReferenceValue = previous;
                data.FindProperty("nextButton").objectReferenceValue = next;
                data.FindProperty("launchButton").objectReferenceValue = launch;
                data.FindProperty("primaryPreviousButton").objectReferenceValue = primaryPrevious;
                data.FindProperty("primaryNextButton").objectReferenceValue = primaryNext;
                data.FindProperty("ordnancePreviousButton").objectReferenceValue = ordnancePrevious;
                data.FindProperty("ordnanceNextButton").objectReferenceValue = ordnanceNext;
                data.FindProperty("launchLegend").objectReferenceValue = parts["LaunchLegend"];
                data.FindProperty("compositeImage").objectReferenceValue = image;
                data.FindProperty("projector").objectReferenceValue = projectorObject.GetComponent<HangarHologramProjector>();
                data.FindProperty("mainDisplay").objectReferenceValue = mainView.GetComponent<HangarDisplay>();
                data.FindProperty("detailDisplay").objectReferenceValue = detailView.GetComponent<HangarDisplay>();
                data.FindProperty("primaryDisplay").objectReferenceValue = primaryView.GetComponent<HangarDisplay>();
                data.FindProperty("ordnanceDisplay").objectReferenceValue = ordnanceView.GetComponent<HangarDisplay>();
                data.FindProperty("mainView").objectReferenceValue = mainView;
                data.FindProperty("detailView").objectReferenceValue = detailView;
                data.FindProperty("primaryView").objectReferenceValue = primaryView;
                data.FindProperty("ordnanceView").objectReferenceValue = ordnanceView;
                data.FindProperty("displayProfile").objectReferenceValue = DisplayProfile();
                data.FindProperty("lightProfile").objectReferenceValue = LightProfile();
                var painted = parts.Values.Where(renderer => renderer.sharedMaterial == paint).ToArray();
                var paintedProperty = data.FindProperty("paintedParts");
                paintedProperty.arraySize = painted.Length;
                for (var i = 0; i < painted.Length; i++)
                    paintedProperty.GetArrayElementAtIndex(i).objectReferenceValue = painted[i];
                data.FindProperty("launchSurface").objectReferenceValue = parts["LaunchButton"];
                data.FindProperty("hologramLightOffset").vector3Value = new Vector3(.05f, 2.774f, -.21f);
                data.FindProperty("launchLightOffset").vector3Value = new Vector3(.026f, 2.971f, -.081f);
                AssignGlows(data.FindProperty("lampGlows"), lampGlows);
                AssignGlows(data.FindProperty("overheadGlows"), overheadGlows);
                AssignGlow(data.FindProperty("lensGlow"), lensGlow);
                data.FindProperty("knobAxis").vector3Value = knobAxis;
                data.FindProperty("knobDetentDegrees").floatValue = 40f;
                data.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(true);
                foreach (var display in root.GetComponentsInChildren<HangarDisplay>(true))
                    display.DisplayCamera.targetTexture = null;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
        }

        private static Vector2Int Resolution(ExportManifest manifest, string glassName)
        {
            var glass = ExportedGlass(manifest, glassName);
            return new Vector2Int(Mathf.RoundToInt(glass.width * PixelsPerUnit),
                Mathf.RoundToInt(glass.height * PixelsPerUnit));
        }

        private static Rect Content(ExportManifest manifest, string glassName)
        {
            var content = ExportedGlass(manifest, glassName).content;
            if (content == null || content.right <= content.left || content.top <= content.bottom)
                throw new InvalidOperationException(
                    $"The hangar console export names no content rectangle for '{glassName}'.");
            return new Rect(content.left, content.bottom, content.right - content.left, content.top - content.bottom);
        }

        private static ExportScreenGlass ExportedGlass(ExportManifest manifest, string glassName)
        {
            if (manifest.screenGlasses != null)
                for (var i = 0; i < manifest.screenGlasses.Length; i++)
                {
                    var entry = manifest.screenGlasses[i];
                    if (entry.name == glassName) return entry;
                }
            throw new InvalidOperationException($"The hangar console export names no screen glass '{glassName}'.");
        }

        private static Material Glass(string name)
        {
            var material = ShaderMaterial(name, "Hangar/ConsolePaint");
            material.SetTexture("_BaseMap", null);
            material.SetTexture("_EmissionMap", null);
            material.SetColor("_Starlight", Color.black);
            material.SetFloat("_LightResponse", 0f);
            material.SetColor("_EmissionTint", new Color(.90f, .95f, 1f));
            material.SetFloat("_EmissionStrength", 1f);
            material.SetFloat("_UseEmissionAlpha", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static HangarDisplayProfile DisplayProfile()
        {
            const string path = "Assets/Settings/UI/HangarDisplayProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<HangarDisplayProfile>(path);
            if (profile) return profile;
            profile = ScriptableObject.CreateInstance<HangarDisplayProfile>();
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        private static HangarLightProfile LightProfile()
        {
            const string path = "Assets/Settings/UI/HangarLightProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<HangarLightProfile>(path);
            if (profile) return profile;
            profile = ScriptableObject.CreateInstance<HangarLightProfile>();
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        private static Object CanopyFeature()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectorPath);
            var image = prefab.GetComponentInChildren<CanopyImage>(true);
            var feature = new SerializedObject(image).FindProperty("feature").objectReferenceValue;
            if (!feature)
                throw new InvalidOperationException("The hangar projector's canopy image has no feature.");
            return feature;
        }

        private static HangarControl Control(MeshRenderer part, Vector3 axis, float travelShare, Camera camera)
        {
            var control = part.gameObject.AddComponent<HangarControl>();
            var inward = Vector3.Dot(part.transform.TransformDirection(axis), camera.transform.forward) >= 0f ? axis : -axis;
            var extent = Vector3.Dot(Vector3.Scale(part.GetComponent<MeshFilter>().sharedMesh.bounds.size, inward), inward);
            var data = new SerializedObject(control);
            data.FindProperty("pressAxis").vector3Value = inward;
            data.FindProperty("travel").floatValue = Mathf.Abs(extent) * travelShare;
            data.ApplyModifiedPropertiesWithoutUndo();
            return control;
        }

        // The exporter frames each control on its Blender object, whose up is the control's axis:
        // a keycap's top and the knob's caps face it, so the mesh proves the frame still holds.
        private static Vector3 AuthoredUp(MeshRenderer part)
        {
            var mesh = part.GetComponent<MeshFilter>().sharedMesh;
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var capArea = new float[3];
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var cross = Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]],
                    vertices[triangles[i + 2]] - vertices[triangles[i]]);
                var area = cross.magnitude;
                if (area <= 0f) continue;
                var normal = cross / area;
                for (var axis = 0; axis < 3; axis++)
                    if (Mathf.Abs(normal[axis]) > .99f) capArea[axis] += area;
            }
            if (capArea[1] <= capArea[0] || capArea[1] <= capArea[2])
                throw new InvalidOperationException($"{part.name}'s caps do not face its up axis; the export's pivot frame changed.");
            return Vector3.up;
        }

        private static void PlaceLight(HangarDisplay display, float distance, float range)
        {
            var data = new SerializedObject(display);
            data.FindProperty("lightDistance").floatValue = distance;
            data.FindProperty("lightRange").floatValue = range;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material Material(string name, string paint, string emission)
        {
            var material = ShaderMaterial(name, "Hangar/ConsolePaint");
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{VisualsPath}/{paint}.png"));
            material.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{VisualsPath}/{emission}.png"));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material ShaderMaterial(string name, string shader)
        {
            var path = $"{VisualsPath}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            material = new Material(Shader.Find(shader)) { name = name };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static HangarGlow[] GlowCards(Transform parent, ExportLamp[] entries, Mesh mesh,
            Material material, Camera camera)
        {
            var cards = new HangarGlow[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var rgb = entry.color;
                cards[i] = GlowCard(parent, entry.name + " glow", ToUnity(entry.position),
                    new Color(rgb[0], rgb[1], rgb[2]), FaceSize(entry), mesh, material, camera, .04f);
            }
            return cards;
        }

        private static HangarGlow GlowCard(Transform parent, string name, Vector3 position, Color color, Vector2 size,
            Mesh mesh, Material material, Camera camera, float push)
        {
            var card = new GameObject(name);
            card.transform.SetParent(parent, false);
            card.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = card.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            card.transform.localPosition = position + (camera.transform.localPosition - position).normalized * push;
            card.transform.rotation = camera.transform.rotation;
            return new HangarGlow { card = renderer, color = color, size = size };
        }

        private static Vector2 FaceSize(ExportLamp entry)
        {
            var size = entry.size;
            if (size == null || size.Length != 3)
                throw new InvalidOperationException($"console-export.json names no size for '{entry.name}'.");
            var span = new[] { size[0], size[1], size[2] };
            Array.Sort(span);
            return new Vector2(span[2], span[1]);
        }

        private static void AssignGlows(SerializedProperty property, HangarGlow[] cards)
        {
            property.arraySize = cards.Length;
            for (var i = 0; i < cards.Length; i++)
                AssignGlow(property.GetArrayElementAtIndex(i), cards[i]);
        }

        private static void AssignGlow(SerializedProperty element, HangarGlow glow)
        {
            element.FindPropertyRelative("card").objectReferenceValue = glow.card;
            element.FindPropertyRelative("color").colorValue = glow.color;
            element.FindPropertyRelative("size").vector2Value = glow.size;
        }

        private static Vector3 ToUnity(float[] value) => new(value[0], value[2], value[1]);

        [Serializable]
        private sealed class ExportManifest
        {
            public ExportCamera camera;
            public ExportScreenGlass[] screenGlasses;
            public ExportLamp[] lamps;
            public ExportLamp[] downlights;
        }

        [Serializable]
        private sealed class ExportLamp
        {
            public string name;
            public float[] position;
            public float[] color;
            public float[] size;
        }

        [Serializable]
        private sealed class ExportScreenGlass
        {
            public string name;
            public float width;
            public float height;
            public ExportContent content;
        }

        [Serializable]
        private sealed class ExportContent
        {
            public float left, right, bottom, top;
        }

        [Serializable]
        private sealed class ExportCamera
        {
            public float[] position;
            public float[] forward;
            public float[] up;
            public float verticalFieldOfView;
        }
    }
}

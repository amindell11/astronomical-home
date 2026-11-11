using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UI.Hangar.Hologram;
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

        [MenuItem("Tools/Hangar/Rebuild console hardware")]
        public static void Rebuild()
        {
            var manifestPath = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../../art/environments/hangar-console/console-export.json"));
            var authored = JsonUtility.FromJson<ExportManifest>(File.ReadAllText(manifestPath)).camera;
            foreach (var filename in new[] { "ConsolePaint", "ConsoleEmission", "MainScreenReference", "DetailScreenReference", "WindowEtching" })
            {
                var path = $"{VisualsPath}/{filename}.png";
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = filename != "WindowEtching";
                importer.alphaSource = filename.EndsWith("ScreenReference", StringComparison.Ordinal)
                    ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
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
            modelImporter.isReadable = false;
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
                var main = Material("MainScreenGlass", "MainScreenReference", "MainScreenReference");
                var detail = Material("DetailScreenGlass", "DetailScreenReference", "DetailScreenReference");
                main.SetColor("_Starlight", Color.black);
                main.SetFloat("_LightResponse", 0f);
                detail.SetColor("_Starlight", Color.black);
                detail.SetFloat("_LightResponse", 0f);
                main.SetColor("_EmissionTint", new Color(1f, .84f, .63f));
                detail.SetColor("_EmissionTint", new Color(1f, .84f, .63f));
                main.SetFloat("_EmissionStrength", 1f);
                detail.SetFloat("_EmissionStrength", 1f);
                main.SetFloat("_UseEmissionAlpha", 1f);
                detail.SetFloat("_UseEmissionAlpha", 1f);
                var parts = model.GetComponentsInChildren<MeshRenderer>().ToDictionary(renderer => renderer.name);
                foreach (var renderer in parts.Values)
                {
                    renderer.sharedMaterial = renderer.name == "MainScreenGlass" ? main
                        : renderer.name == "DetailScreenGlass" ? detail : paint;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                var legend = ShaderMaterial("LaunchLegend", "Hangar/ConsolePaint");
                legend.SetTexture("_EmissionMap", null);
                legend.SetColor("_Starlight", Color.black);
                legend.SetFloat("_LightResponse", 0f);
                legend.SetColor("_ConstantEmission", new Color(1f, .88f, .70f));
                parts["LaunchLegend"].sharedMaterial = legend;
                parts["LaunchLegend"].transform.SetParent(parts["LaunchButton"].transform, true);

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
                Light(root.transform, "MainScreenSpill", new Vector3(-5.4f, 3.4f, -2.5f),
                    new Color(1f, .87f, .70f), 4.55f, 8f);
                Light(root.transform, "ProjectorSpill", new Vector3(2.4f, 4.4f, -.2f),
                    new Color(1f, .87f, .70f), 8.45f, 12f);
                Light(root.transform, "DetailScreenSpill", new Vector3(-5f, 1.1f, -3f),
                    new Color(1f, .87f, .70f), 1.3f, 5f);
                Light(root.transform, "LaunchSpill", new Vector3(6.5f, 4f, -2.3f),
                    new Color(1f, .87f, .70f), 2.5f, 6f);
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

                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = 5;

                var data = new SerializedObject(console);
                data.FindProperty("hardwareCamera").objectReferenceValue = camera;
                data.FindProperty("referenceVerticalFov").floatValue = authored.verticalFieldOfView;
                foreach (var name in new[] { "MainScreenGlass", "DetailScreenGlass", "ProjectorLens" })
                    data.FindProperty(char.ToLowerInvariant(name[0]) + name.Substring(1)).objectReferenceValue = parts[name];
                foreach (var name in new[] { "SelectorKnob", "PreviousButton", "NextButton", "LaunchButton" })
                    data.FindProperty(char.ToLowerInvariant(name[0]) + name.Substring(1)).objectReferenceValue = parts[name].transform;
                data.FindProperty("compositeImage").objectReferenceValue = image;
                data.FindProperty("projector").objectReferenceValue = projectorObject.GetComponent<HangarHologramProjector>();
                data.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
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

        private static void Light(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.cullingMask = 1 << 5;
            light.shadows = LightShadows.None;
        }

        private static Vector3 ToUnity(float[] value) => new(value[0], value[2], value[1]);

        [Serializable]
        private sealed class ExportManifest
        {
            public ExportCamera camera;
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

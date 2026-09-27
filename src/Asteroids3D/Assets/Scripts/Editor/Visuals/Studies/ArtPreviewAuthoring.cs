using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Visuals.Studies
{
    public static class ArtPreviewAuthoring
    {
        public const string Folder = "Assets/Visuals/Studies/DrawnArt/";
        private const string ShipFolder = "Assets/Visuals/Ships/Vanguard/DrawnStudy/";
        private const string RockFolder = "Assets/Visuals/Environment/Asteroids/DrawnField/";

        [MenuItem("Astronomical/Art previews/Rebuild approved study scenes")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (var directory in new[] { "Scenes", "Prefabs", "Materials/Ship/Panels", "Materials/Ship/Surface",
                "Materials/Ship/Outline", "Materials/Stage", "Meshes/Hull", "Meshes/Engines", "Meshes/Wings", "Lighting", "Textures" })
                Directory.CreateDirectory(Folder + directory);
            AssetDatabase.Refresh();
            var ship = BuildShip();
            var field = BuildField();
            var profile = BuildBloom();
            BuildScene("HangarHero", ship, field, profile, "HangarBackground-v2-ui.png", true, false);
            BuildScene("SpaceHero", ship, field, profile, "PlanetBackground-v2-ui.png", false, false);
            BuildScene("AsteroidField", ship, field, profile, "NebulaBackground-v2.png", false, true);
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(Folder + "Scenes/HangarHero.unity");
        }

        private static GameObject BuildShip()
        {
            var root = new GameObject("Vanguard art preview");
            var ship = Object.Instantiate(Load<GameObject>(ShipFolder + "VanguardStructure.fbx"), root.transform);
            ship.name = "Authored mesh";
            ship.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var all = ship.GetComponentsInChildren<MeshRenderer>();
            var hull = Array.FindAll(all, r => r.name != "Vanguard structural ink" &&
                r.name != "Vanguard service panels" && r.name != "Vanguard surface wear");
            var colors = new[] { new Color(.38f, .39f, .41f), new Color(.62f, .62f, .59f),
                new Color(.80f, .78f, .72f), new Color(.39f, .46f, .54f), new Color(.95f, .64f, .18f) };
            var names = new[] { "Vent charcoal", "Vent slats", "Panel edges", "Inner blue gray", "Amber detail" };
            var panels = new Material[colors.Length];
            for (var i = 0; i < colors.Length; i++)
            {
                var material = Paint(colors[i].linear);
                panels[i] = Save(material, "Materials/Ship/Panels/" + names[i] + ".mat");
            }
            var ink = new Material(Shader.Find("Astronomical/Comparison/Drawn Surface"));
            ink.SetFloat("_TextureStrength", 1);
            ink.SetFloat("_LineStrength", 0);
            ink.SetFloat("_SpecularStrength", 0);
            ink.SetColor("_BaseColor", new Color(.003f, .004f, .009f));
            var wear = new Material(ink);
            wear.SetColor("_BaseColor", new Color(.62f, .60f, .57f).linear);
            ink = Save(ink, "Materials/Ship/Surface/Structural ink.mat");
            wear = Save(wear, "Materials/Ship/Surface/Battle wear.mat");
            foreach (var renderer in all)
            {
                renderer.gameObject.layer = 30;
                if (renderer.name == "Vanguard service panels") renderer.sharedMaterials = panels;
                else if (renderer.name == "Vanguard structural ink") renderer.sharedMaterial = ink;
                else if (renderer.name == "Vanguard surface wear") renderer.sharedMaterial = wear;
                else continue;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            var texture = Load<Texture2D>(ShipFolder + "VanguardBaseColor.png");
            var paint = Paint(Color.white);
            paint.SetTexture("_BaseMap", texture);
            var source = new Color(234 / 255f, 148 / 255f, 31 / 255f).linear;
            var reference = new Color(251 / 255f, 135 / 255f, 22 / 255f).linear;
            paint.SetVector("_OrangeGain", new Vector4(reference.r / source.r, reference.g / source.g, reference.b / source.b, 0));
            paint = Save(paint, "Materials/Ship/Surface/Hull paint.mat");
            var canopy = Save(Paint(new Color(.025f, .065f, .15f)), "Materials/Ship/Surface/Canopy.mat");
            var pod = Paint(Color.black);
            var emissionMask = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            emissionMask.SetPixel(0, 0, Color.white);
            emissionMask.Apply();
            pod.SetTexture("_EmissionMap", Save(emissionMask, "Textures/EngineEmissionMask.asset"));
            pod.SetColor("_EmissionColor", new Color(.15f, 1.8f, 6));
            pod.SetFloat("_EmissionStrength", 1);
            pod = Save(pod, "Materials/Ship/Surface/Engine glow.mat");
            var contour = Contour(7.5f, .8f, Color.black);
            contour.SetFloat("_UniformWidth", 1);
            contour = Save(contour, "Materials/Ship/Outline/Hero outline.mat");
            var gameplayContour = new Material(contour);
            gameplayContour.SetFloat("_ContourPixels", 3.25f);
            Save(gameplayContour, "Materials/Ship/Outline/Gameplay outline.mat");
            foreach (var renderer in hull)
            {
                renderer.sharedMaterial = renderer.name.Contains("Nacelle Core") ? pod :
                    renderer.name.Contains("Canopy") ? canopy : paint;
                var mesh = Object.Instantiate(renderer.GetComponent<MeshFilter>().sharedMesh);
                var vertices = mesh.vertices;
                var normals = mesh.normals;
                var joined = new Dictionary<Vector3, Vector3>();
                for (var v = 0; v < vertices.Length; v++)
                {
                    joined.TryGetValue(vertices[v], out var normal);
                    joined[vertices[v]] = normal + normals[v];
                }
                for (var v = 0; v < vertices.Length; v++) normals[v] = joined[vertices[v]].normalized;
                mesh.normals = normals;
                var group = renderer.name.Contains("Nacelle") ? "Engines" :
                    renderer.name.Contains("Canopy") || renderer.name.Contains("Cockpit") || renderer.name.Contains("Fuselage") ? "Hull" : "Wings";
                mesh = Save(mesh, "Meshes/" + group + "/" + renderer.name + " outline.asset");
                Layer("Silhouette", renderer.transform, mesh, contour, false);
            }
            var bounds = hull[0].bounds;
            foreach (var renderer in hull) bounds.Encapsulate(renderer.bounds);
            ship.transform.localScale *= 6 / bounds.size.y;
            bounds = hull[0].bounds;
            foreach (var renderer in hull) bounds.Encapsulate(renderer.bounds);
            ship.transform.position -= bounds.center;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "Prefabs/VanguardPreview.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildField()
        {
            var root = new GameObject("Asteroid context");
            var graphite = Load<Material>(RockFolder + "FieldGraphite.mat");
            var contour = Save(Contour(7.5f, .85f, new Color(.003f, .004f, .009f)), "Materials/Stage/Asteroid outline.mat");
            var positions = new[] { new Vector2(-29, -17), new Vector2(-18, 10), new Vector2(-8, 18),
                new Vector2(7, 20), new Vector2(22, 12), new Vector2(32, -7), new Vector2(13, -12),
                new Vector2(-13, -10), new Vector2(-32, 2), new Vector2(0, -21) };
            for (var i = 0; i < positions.Length; i++)
            {
                var path = RockFolder + $"Shape{i + 1:D2}/Asteroid{i + 1}";
                var mesh = Load<GameObject>(path + ".fbx").GetComponentInChildren<MeshFilter>().sharedMesh;
                var drawing = Load<GameObject>(path + "Drawing.fbx").GetComponentInChildren<MeshFilter>().sharedMesh;
                var rock = Layer("Painted asteroid " + (i + 1), root.transform, mesh, Load<Material>(path + "Paint.mat"), true);
                Layer("Crease drawing", rock.transform, drawing, graphite, false).GetComponent<MeshRenderer>().receiveShadows = true;
                Layer("Outer contour", rock.transform, mesh, contour, false);
                rock.transform.rotation = Quaternion.Euler(i * 37, i * 61, i * 23);
                var size = rock.GetComponent<MeshRenderer>().bounds.size;
                rock.transform.localScale *= (6 + i % 4) / Mathf.Max(size.x, size.y, size.z);
                rock.transform.position = positions[i];
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "Prefabs/AsteroidContext.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static VolumeProfile BuildBloom()
        {
            var path = Folder + "Lighting/PodBloom.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            if (!profile.TryGet<Bloom>(out var bloom))
            {
                bloom = profile.Add<Bloom>(true);
                AssetDatabase.AddObjectToAsset(bloom, profile);
            }
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(.8f);
            bloom.scatter.Override(.65f);
            EditorUtility.SetDirty(bloom);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void BuildScene(string name, GameObject ship, GameObject field, VolumeProfile profile,
            string background, bool hangar, bool gameplay)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.22f, .25f, .32f);
            var light = new GameObject("Fixed key", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1;
            light.shadows = LightShadows.Soft;
            light.shadowBias = .02f;
            light.shadowNormalBias = .05f;
            light.cullingMask = 1 << 30;
            light.transform.rotation = Quaternion.Euler(25, -35, 0);
            RenderSettings.sun = light;
            var camera = new GameObject("Preview camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = gameplay ? 26 : 3.5f;
            camera.transform.position = new Vector3(0, 0, -60);
            camera.allowHDR = true;
            camera.allowMSAA = true;
            camera.nearClipPlane = 50;
            camera.farClipPlane = 70;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.045f, .062f, .105f);
            camera.cullingMask = 1 << 30;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.volumeLayerMask = 1 << 30;
            var volume = new GameObject("Pod bloom", typeof(Volume)).GetComponent<Volume>();
            volume.gameObject.layer = 30;
            volume.isGlobal = true;
            volume.priority = 100;
            volume.sharedProfile = profile;
            var subject = new GameObject("Subject — replace child asset");
            subject.transform.SetPositionAndRotation(gameplay ? new Vector3(0, -3, 0) : new Vector3(1.8f, -.3f, 0),
                gameplay ? Quaternion.Euler(0, 0, 155) : Quaternion.Euler(55, -10, -50));
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(ship, subject.transform);
            if (gameplay)
            {
                var outline = Load<Material>(Folder + "Materials/Ship/Outline/Gameplay outline.mat");
                foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
                {
                    if (renderer.name != "Silhouette") continue;
                    renderer.sharedMaterial = outline;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                PrefabUtility.InstantiatePrefab(field);
            }
            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "Reference backdrop — baked UI";
            backdrop.layer = 30;
            Object.DestroyImmediate(backdrop.GetComponent<Collider>());
            backdrop.transform.position = new Vector3(0, 0, hangar ? 3 : 8);
            backdrop.transform.localScale = new Vector3(2 * camera.orthographicSize * 16 / 9, 2 * camera.orthographicSize, 1);
            var material = new Material(Shader.Find(hangar ? "Astronomical/Comparison/Shadowed Plate" : "Universal Render Pipeline/Unlit"));
            material.SetTexture("_BaseMap", Load<Texture2D>(ShipFolder + background));
            if (hangar) material.SetColor("_ShadowColor", new Color(.3f, .35f, .55f));
            var rendererPlate = backdrop.GetComponent<MeshRenderer>();
            rendererPlate.sharedMaterial = Save(material, "Materials/Stage/" + name + " backdrop.mat");
            rendererPlate.shadowCastingMode = ShadowCastingMode.Off;
            rendererPlate.receiveShadows = hangar;
            EditorSceneManager.SaveScene(scene, Folder + "Scenes/" + name + ".unity");
        }

        private static Material Paint(Color color)
        {
            var material = new Material(Shader.Find("Astronomical/Comparison/Drawn Surface"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_TextureStrength", 1);
            material.SetFloat("_LineStrength", 0);
            material.SetFloat("_WearStrength", 0);
            material.SetFloat("_SpecularStrength", 0);
            material.SetFloat("_ShadowThreshold", .55f);
            material.SetFloat("_ShadowSoftness", .18f);
            material.SetFloat("_PaletteLighting", 1);
            material.SetFloat("_AmbientStrength", .35f);
            material.SetFloat("_CastShadowStrength", .96f);
            material.SetColor("_ShadowColor", new Color(.35f, .45f, .8f));
            return material;
        }

        private static Material Contour(float pixels, float minimum, Color color)
        {
            var material = new Material(Shader.Find("Astronomical/Comparison/Drawn Contour"));
            material.SetFloat("_ContourPixels", pixels);
            material.SetFloat("_ContourMinimum", minimum);
            material.SetColor("_ContourColor", color);
            return material;
        }

        private static GameObject Layer(string name, Transform parent, Mesh mesh, Material material, bool shadows)
        {
            var item = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)) { layer = 30 };
            item.transform.SetParent(parent, false);
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = shadows;
            return item;
        }

        private static T Save<T>(T asset, string relative) where T : Object
        {
            var path = Folder + relative;
            asset.name = Path.GetFileNameWithoutExtension(relative);
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!existing)
            {
                AssetDatabase.CreateAsset(asset, path);
                return asset;
            }
            EditorUtility.CopySerialized(asset, existing);
            Object.DestroyImmediate(asset);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset) throw new InvalidOperationException("Missing art preview input: " + path);
            return asset;
        }
    }
}

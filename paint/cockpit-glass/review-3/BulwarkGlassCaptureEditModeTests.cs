using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tests.EditMode.Rendering.Illustrated
{
    [Category("Ships"), Category("RequiresGraphics")]
    public sealed class BulwarkGlassCaptureEditModeTests
    {
        [Serializable] public sealed class MeshDump
        {
            public Vector3[] vertices;
            public Vector3[] normals;
            public Vector2[] uv;
            public int[] triangles;
        }

        [Test]
        public void Capture()
        {
            const string output = "D:/amind/git/agent-8/scratch/bulwark-glass/";
            string stage = File.ReadAllText(output + "stage.txt").Trim();
            var priorPipeline = QualitySettings.renderPipeline;
            var priorSun = RenderSettings.sun;
            bool priorAsync = ShaderUtil.allowAsyncCompilation;
            var priorTarget = RenderTexture.active;
            var priorAmbient = RenderSettings.ambientLight;
            var priorAmbientMode = RenderSettings.ambientMode;
            var root = new GameObject("Bulwark glass capture");
            var texture = new Texture2D(640, 640, TextureFormat.RGB24, false);
            var target = new RenderTexture(640, 640, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try
            {
                QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/Rendering/URP-HighFidelity.asset");
                ShaderUtil.allowAsyncCompilation = false;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.24f, .24f, .24f);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Visuals/Ships/Bulwark/Bulwark Visual.prefab");
                var ship = UnityEngine.Object.Instantiate(asset, root.transform);
                foreach (var child in ship.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                var canopy = ship.GetComponentsInChildren<MeshRenderer>().Single(r => r.name == "Canopy");
                Dump(canopy, 0, output + "Bulwark-unity.json");
                var debris = PrefabUtility.LoadPrefabContents("Assets/Visuals/Ships/Bulwark/Breakup/BulwarkBreakup.prefab");
                try
                {
                    var cockpit = debris.GetComponentsInChildren<MeshRenderer>().Single(r => r.sharedMaterials.Any(m => m.name.Contains("Glass")));
                    Dump(cockpit, 0, output + "Bulwark-debris.json");
                }
                finally { PrefabUtility.UnloadPrefabContents(debris); }
                var key = new GameObject("Capture key", typeof(Light)).GetComponent<Light>();
                key.transform.SetParent(root.transform);
                key.type = LightType.Directional;
                key.intensity = 1.6f;
                key.cullingMask = 1 << 31;
                key.transform.rotation = Quaternion.Euler(20, -25, 0);
                RenderSettings.sun = key;
                var camera = new GameObject("Capture camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform);
                camera.orthographic = true;
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.035f, .04f, .05f);
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.nearClipPlane = .01f;
                camera.farClipPlane = 100;
                camera.targetTexture = target;
                var bounds = ship.GetComponentsInChildren<Renderer>().Where(r => r.enabled).Select(r => r.bounds).ToArray();
                var full = bounds[0];
                foreach (var bound in bounds) full.Encapsulate(bound);
                camera.orthographicSize = Mathf.Max(full.extents.x, full.extents.y) * 1.12f;
                camera.transform.position = new Vector3(full.center.x, full.center.y, -30);
                Save(camera, texture, target, output + stage + "-ship.png");
                Directory.CreateDirectory(output + stage);
                var originalGlass = canopy.sharedMaterial;
                var noReflections = new Material(originalGlass);
                noReflections.SetFloat("_DrawnReflectionStrength", 0);
                canopy.sharedMaterial = noReflections;
                camera.orthographicSize = Mathf.Max(canopy.bounds.extents.x, canopy.bounds.extents.y) * 1.4f;
                camera.transform.position = new Vector3(canopy.bounds.center.x, canopy.bounds.center.y, -30);
                Save(camera, texture, target, output + stage + "-reflections-off.png");
                canopy.sharedMaterial = originalGlass;
                UnityEngine.Object.DestroyImmediate(noReflections);
                camera.orthographicSize = Mathf.Max(canopy.bounds.extents.x, canopy.bounds.extents.y) * 1.4f;
                for (int frame = 0; frame < 160; frame++)
                {
                    float phase = (frame % 80) * Mathf.PI * 2 / 80;
                    ship.transform.rotation = frame < 80
                        ? Quaternion.Euler(0, Mathf.Sin(phase) * 55, 0)
                        : Quaternion.Euler(Mathf.Sin(phase) * 35, 0, 0);
                    var center = canopy.bounds.center;
                    camera.transform.position = new Vector3(center.x, center.y, -30);
                    Save(camera, texture, target, output + stage + "/" + frame.ToString("D3") + ".png");
                }
                Assert.That(texture.GetPixels().Select(c => c.grayscale).Distinct().Count(), Is.GreaterThan(100));
            }
            finally
            {
                QualitySettings.renderPipeline = priorPipeline;
                RenderSettings.sun = priorSun;
                ShaderUtil.allowAsyncCompilation = priorAsync;
                RenderTexture.active = priorTarget;
                RenderSettings.ambientLight = priorAmbient;
                RenderSettings.ambientMode = priorAmbientMode;
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void Dump(MeshRenderer renderer, int submesh, string path)
        {
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            File.WriteAllText(path, JsonUtility.ToJson(new MeshDump { vertices = mesh.vertices, normals = mesh.normals, uv = mesh.uv, triangles = mesh.GetTriangles(submesh) }));
        }

        private static void Save(Camera camera, Texture2D image, RenderTexture target, string path)
        {
            camera.Render();
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
    }
}

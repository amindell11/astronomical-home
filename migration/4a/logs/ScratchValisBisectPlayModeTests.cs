#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Substrate;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Presentation
{
    [Category("Presentation"), Category("RequiresGraphics")]
    public sealed class ScratchValisBisectPlayModeTests
    {
        private const int Size = 768;
        private const string OldPath = "Assets/ValisBisectOld.prefab";
        private const string NewPath = "Assets/Prefabs/Ships/Valis.prefab";
        private static readonly string Out = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../results/ship-render/bisect"));

        [Test]
        public void Bisect()
        {
            Directory.CreateDirectory(Out);
            var report = new StringBuilder();
            var renders = new Dictionary<string, Color32[]>();
            void Run(string name, string path, Action<GameObject> mutate, string hullPath)
            {
                renders[name] = Render(name, path, mutate, hullPath, report);
            }
            const string oldHull = "Valis VisualRig/Valis";
            const string newHull = "ShipBaseRig/Hull/Valis";
            var pairs = new List<(string, string)>();
            foreach (var swept in new[] { false, true })
                foreach (var yaw in new[] { 0, 90, 135 })
                {
                    var tag = $"{yaw}{(swept ? "-swept" : "")}";
                    renders["O-" + tag] = Render("O-" + tag, OldPath, null, oldHull, report, yaw, swept);
                    renders["N-" + tag] = Render("N-" + tag, NewPath, null, newHull, report, yaw, swept);
                    pairs.Add(("O-" + tag, "N-" + tag));
                }
            var forward = new List<Material>();
            var backward = new List<Material>();
            renders["N-clonesForward"] = Render("N-clonesForward", NewPath, s => Reclone(s, newHull, false, forward), newHull, report);
            renders["N-clonesBackward"] = Render("N-clonesBackward", NewPath, s => Reclone(s, newHull, true, backward), newHull, report);
            renders["O-clonesBackward"] = Render("O-clonesBackward", OldPath, s => Reclone(s, oldHull, true, backward), oldHull, report);
            pairs.Add(("N-0", "N-clonesForward")); pairs.Add(("N-0", "N-clonesBackward")); pairs.Add(("N-clonesForward", "N-clonesBackward")); pairs.Add(("O-0", "O-clonesBackward"));
            foreach (var (a, b) in pairs)
            {
                var changed = 0; var max = 0;
                for (var i = 0; i < renders[a].Length; i++)
                {
                    var d = Math.Abs(renders[a][i].r - renders[b][i].r) + Math.Abs(renders[a][i].g - renders[b][i].g) + Math.Abs(renders[a][i].b - renders[b][i].b);
                    max = Math.Max(max, d); if (d > 6) changed++;
                }
                report.AppendLine($"{a} vs {b}: changed={changed} max={max}");
            }
            report.AppendLine("forward clone ids: " + string.Join(",", forward.Select(m => m.name + "=" + m.GetInstanceID())));
            report.AppendLine("backward clone ids: " + string.Join(",", backward.Select(m => m.name + "=" + m.GetInstanceID())));
            File.WriteAllText(Path.Combine(Out, "bisect.txt"), report.ToString());
            Debug.Log(report.ToString());
        }

        private static void Reclone(GameObject subject, string hullPath, bool reverse, List<Material> made)
        {
            var smr = subject.transform.Find(hullPath).GetComponent<SkinnedMeshRenderer>();
            var shared = smr.sharedMaterials;
            var clones = new Material[shared.Length];
            var order = Enumerable.Range(0, shared.Length);
            if (reverse) order = order.Reverse();
            foreach (var i in order) { clones[i] = new Material(shared[i]) { name = shared[i].name }; made.Add(clones[i]); }
            smr.sharedMaterials = clones;
        }

        private static void OnlyHull(GameObject subject, string hullPath)
        {
            var hull = subject.transform.Find(hullPath);
            foreach (var r in subject.GetComponentsInChildren<Renderer>(true))
                if (!r.transform.IsChildOf(hull)) Object.DestroyImmediate(r);
        }

        private static void Extract(GameObject subject, string hullPath)
        {
            var hull = subject.transform.Find(hullPath);
            hull.SetParent(subject.transform, false);
            foreach (Transform child in subject.transform.Cast<Transform>().ToArray())
                if (child != hull) Object.DestroyImmediate(child.gameObject);
            foreach (var c in subject.GetComponents<Component>().Reverse())
                if (!(c is Transform) && !(c is Rigidbody)) Object.DestroyImmediate(c);
            Object.DestroyImmediate(subject.GetComponent<Rigidbody>());
        }

        private static Color32[] Render(string name, string chassisPath, Action<GameObject> mutate, string hullPath, StringBuilder report, int yaw = 0, bool swept = false)
        {
            var root = new GameObject("Chassis render");
            var priorTarget = RenderTexture.active;
            var priorAmbient = RenderSettings.ambientLight;
            var priorMode = RenderSettings.ambientMode;
            var target = new RenderTexture(Size, Size, 24);
            var image = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            try
            {
                var prefab = AssetDatabase.LoadMainAssetAtPath(chassisPath) as GameObject;
                Assert.That(prefab, Is.Not.Null, chassisPath);
                root.SetActive(false);
                var subject = Object.Instantiate(prefab, root.transform);
                if (swept)
                    foreach (var wing in subject.GetComponentsInChildren<Ships.Visuals.Wings.WingVisuals>(true))
                    {
                        var joints = new SerializedObject(wing).FindProperty("joints");
                        for (var i = 0; i < joints.arraySize; i++)
                            ((Transform)joints.GetArrayElementAtIndex(i).FindPropertyRelative("bone").objectReferenceValue).localRotation = Quaternion.identity;
                    }
                foreach (var behaviour in subject.GetComponentsInChildren<MonoBehaviour>(true))
                    if (!(behaviour.GetType().Namespace ?? "").StartsWith("UnityEngine", StringComparison.Ordinal))
                        Object.DestroyImmediate(behaviour);
                foreach (var particles in subject.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    particles.enabled = false;
                mutate?.Invoke(subject);
                subject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0, 0, yaw));
                var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                camera.transform.position = new Vector3(0, 0, -6);
                camera.transform.LookAt(Vector3.zero, Vector3.up);
                camera.orthographic = true;
                camera.orthographicSize = 1.4f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.25f, .28f, .35f);
                camera.cullingMask = 1 << LayerIds.Ship;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.targetTexture = target;
                var light = new GameObject("Key light", typeof(Light)).GetComponent<Light>();
                light.transform.SetParent(root.transform, false);
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(25, -35, 0);
                light.color = new Color(1, .96f, .9f);
                light.intensity = 1.15f;
                light.cullingMask = 1 << LayerIds.Ship;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.22f, .25f, .32f);
                root.SetActive(true);
                var hull = subject.transform.Find(hullPath);
                var smr = hull.GetComponent<SkinnedMeshRenderer>();
                var shipLayer = subject.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled && r.gameObject.layer == LayerIds.Ship).Select(r => r.name + ":" + r.GetType().Name);
                report.Append($"{name}: shipLayerRenderers=[{string.Join(",", shipLayer)}] l2w={smr.localToWorldMatrix.ToString("R").Replace("\n", " ")} root={smr.rootBone.name} bounds={smr.bounds.ToString("R")} localBounds={smr.localBounds.ToString("R")} sortingLayer={smr.sortingLayerID}/{smr.sortingOrder} prio={smr.rendererPriority} motion={smr.motionVectorGenerationMode}/{smr.skinnedMotionVectors} probe={smr.lightProbeUsage}/{smr.reflectionProbeUsage} quality={smr.quality} offscreen={smr.updateWhenOffscreen}\n");
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(Out, name + ".png"), image.EncodeToPNG());
                return image.GetPixels32();
            }
            finally
            {
                RenderTexture.active = priorTarget;
                RenderSettings.ambientLight = priorAmbient;
                RenderSettings.ambientMode = priorMode;
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
            }
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Capture;
using Capture.Recording;
using Capture.Views;
using Cameras;
using Game;
using Game.Preview;
using NUnit.Framework;
using Ships;
using Substrate.Sectors.Elements;
using Substrate.Services.Locales;
using Tests.PlayMode.Recording;
using UI.Hangar.Hardware;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Scratch
{
    // Scratch for #1593: calibrates each light's linear emission so the flight camera's grading and bloom land it on
    // its target colour, as gate4-calibrate.py did for #1587, in one run.
    public sealed class DressCalibration : CaptureScenario
    {
        private const string Out = @"C:\Users\amind\AppData\Local\Temp\claude\882050741e06a72d\7333cce7-bc83-4523-8f8d-c6070660c379\scratchpad\calibration";
        private const int Iterations = 12;

        private static readonly string[] Targets =
        {
            "#FF9548", "#BA82FF", "#D7E6FF", // windows: Shoal, Veil, Bastion
            "#5FE0C0", "#FFD27A", "#8C9CFF", // service: repair, mod, armory
            "#FEAC39", "#FFE25A",            // #1587's window and prize, as a check against its calibration
        };

        public override bool NeedsSession => false;
        public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;

        public override IEnumerator Run()
        {
            Directory.CreateDirectory(Out);
            var log = new List<string>();
            QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, "High Fidelity"), true);
            var bootstrap = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Game.Preview.PreviewHostBootstrap")).First(t => t != null);
            var build = bootstrap.GetMethod("BuildHost", BindingFlags.NonPublic | BindingFlags.Static);
            var locale = AssetDatabase.LoadAssetAtPath<LocaleSky>("Assets/Prefabs/Locales/CopperTurquoise.prefab");
            var host = (PreviewHost)build.Invoke(null, new object[] { PreviewEntry.Combat, 1, locale, true });
            host.gameObject.SetActive(true);
            yield return WaitFor(() => Object.FindFirstObjectByType<HangarConsole>(), "hangar");
            yield return Frames(30);
            Object.FindFirstObjectByType<HangarConsole>().Launch();
            yield return new WaitForSeconds(8f);

            var rig = Object.FindFirstObjectByType<PlayerRig>();
            foreach (var s in Object.FindObjectsByType<SectorSpawner>(FindObjectsSortMode.None)) s.enabled = false;
            foreach (var ship in Object.FindObjectsByType<Ship>(FindObjectsSortMode.None))
                if (ship != rig.Player) ship.gameObject.SetActive(false);
            var group = rig.Overlay ? rig.Overlay.GetComponent<CanvasGroup>() : null;
            if (group) group.alpha = 0f;
            var observer = Object.FindObjectsByType<ObserverCam>(FindObjectsSortMode.None).First(o => o.Cam && o.Cam.enabled);
            observer.enabled = false;
            var cam = observer.Cam;
            cam.orthographicSize = 15f;
            rig.Player.gameObject.SetActive(false);

            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Visuals/Environment/Stations/Outpost/Materials/Outpost Window.mat");
            var centre = cam.transform.position;
            centre.z = 0f;
            var root = new GameObject("SWATCHES");
            var block = new MaterialPropertyBlock();
            var renderers = new MeshRenderer[Targets.Length];
            var spots = new Vector3[Targets.Length];
            for (var i = 0; i < Targets.Length; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(root.transform);
                spots[i] = centre + new Vector3(-21f + i * 6f, 0f, 0f);
                q.transform.position = spots[i];
                q.transform.localScale = new Vector3(1.2f, 0.8f, 1f);
                renderers[i] = q.GetComponent<MeshRenderer>();
                renderers[i].sharedMaterial = mat;
            }

            var want = Targets.Select(Rgb).ToArray();
            var inputs = want.Select(t => t.Select(Lin).ToArray()).ToArray();
            var got = new float[Targets.Length][];
            for (var it = 0; it <= Iterations; it++)
            {
                for (var i = 0; i < Targets.Length; i++)
                {
                    block.SetVector("_EmissionColor", new Vector4(inputs[i][0], inputs[i][1], inputs[i][2], 1f));
                    renderers[i].SetPropertyBlock(block);
                }
                yield return Frames(4);
                var tex = Shot(cam, Path.Combine(Out, $"swatch-{it}.png"));
                for (var i = 0; i < Targets.Length; i++)
                    got[i] = Sample(tex, cam.WorldToViewportPoint(spots[i]));
                Object.DestroyImmediate(tex);
                log.Add($"iter {it}: " + string.Join("; ", Targets.Select((t, i) =>
                    $"{t} got ({got[i][0]:F0},{got[i][1]:F0},{got[i][2]:F0}) err {Err(want[i], got[i]):F0} in ({inputs[i][0]:F4},{inputs[i][1]:F4},{inputs[i][2]:F4})")));
                if (it == Iterations) break;
                for (var i = 0; i < Targets.Length; i++)
                    for (var c = 0; c < 3; c++)
                        inputs[i][c] = Mathf.Max(1e-4f, inputs[i][c] *
                            Mathf.Pow(Lin(Mathf.Clamp(want[i][c], 1f, 255f)) / Mathf.Max(Lin(Mathf.Max(1f, got[i][c])), 1e-4f), 0.7f));
            }

            File.WriteAllLines(Path.Combine(Out, "log.txt"), log);
            File.WriteAllLines(Path.Combine(Out, "calibration.txt"), Targets.Select((t, i) =>
                $"{t} {inputs[i][0]:F5} {inputs[i][1]:F5} {inputs[i][2]:F5} got {got[i][0]:F0} {got[i][1]:F0} {got[i][2]:F0}"));

            using (CapturePacing.Locked(2))
            using (var recording = Capture.Prepare(new CaptureRequest("results/capture", "dress-calibration", Commit, 1280, 720)))
            {
                yield return recording.Ready;
                recording.Start();
                yield return Frames(4);
                Result = recording.Finish();
            }
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(host.gameObject);
            yield return Frames(10);
        }

        private static float[] Rgb(string hex) =>
            new[] { 1, 3, 5 }.Select(i => (float)Convert.ToInt32(hex.Substring(i, 2), 16)).ToArray();

        private static float Lin(float c)
        {
            c /= 255f;
            return c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        }

        private static float Err(float[] a, float[] b) => Enumerable.Range(0, 3).Max(c => Mathf.Abs(a[c] - b[c]));

        private static float[] Sample(Texture2D tex, Vector3 viewport)
        {
            var x = (int)(viewport.x * tex.width);
            var y = (int)(viewport.y * tex.height);
            var sum = new float[3];
            var n = 0;
            for (var dx = -3; dx <= 3; dx++)
                for (var dy = -3; dy <= 3; dy++)
                {
                    var p = (Color32)tex.GetPixel(x + dx, y + dy);
                    sum[0] += p.r;
                    sum[1] += p.g;
                    sum[2] += p.b;
                    n++;
                }
            return sum.Select(s => s / n).ToArray();
        }

        private static Texture2D Shot(Camera cam, string path)
        {
            var rt = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            var prior = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();
            cam.targetTexture = prior;
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            return tex;
        }

        private static IEnumerator Frames(int count)
        {
            for (var i = 0; i < count; i++) yield return null;
        }

        private static IEnumerator WaitFor(Func<bool> ready, string what)
        {
            var deadline = Time.realtimeSinceStartup + 90f;
            while (!ready())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), $"Timed out waiting for {what}.");
                yield return null;
            }
        }
    }
}
#endif

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
using Substrate.Sectors.Elements.Stations;
using Substrate.Services.Locales;
using Tests.PlayMode.Recording;
using UI.Hangar.Hardware;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Scratch
{
    // Scratch capture for #1588: each swapped station in a real Combat sector, lit and dark, at sizes 15 and 25.
    public sealed class StationSwapStills : CaptureScenario
    {
        private const string Out = @"C:\Users\amind\AppData\Local\Temp\claude\882050741e06a72d\4627d6f6-1936-45db-8771-65f62a4ef8a8\scratchpad\stills";
        private const int Sector = 1;
        private static readonly float[] Zooms = { 15f, 25f };

        public override bool NeedsSession => false;
        public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;

        public override IEnumerator Run()
        {
            Directory.CreateDirectory(Out);
            var log = new List<string>();
            QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, "High Fidelity"), true);
            var bootstrap = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Game.Preview.PreviewHostBootstrap")).FirstOrDefault(t => t != null)
                ?? throw new InvalidOperationException("PreviewHostBootstrap not found");
            var build = bootstrap.GetMethod("BuildHost", BindingFlags.NonPublic | BindingFlags.Static)
                        ?? throw new InvalidOperationException("PreviewHostBootstrap.BuildHost not found");
            var locales = new[] { ("copper", "CopperTurquoise"), ("verdant", "VerdantGiant") };
            for (var l = 0; l < locales.Length; l++)
            {
                var (tag, prefab) = locales[l];
                var locale = AssetDatabase.LoadAssetAtPath<LocaleSky>($"Assets/Prefabs/Locales/{prefab}.prefab");
                var host = (PreviewHost)build.Invoke(null, new object[] { PreviewEntry.Combat, Sector, locale, true });
                host.gameObject.SetActive(true);
                yield return WaitFor(() => Object.FindFirstObjectByType<HangarConsole>(), "hangar");
                yield return Frames(30);
                Object.FindFirstObjectByType<HangarConsole>().Launch();
                yield return WaitFor(() => Kinds().All(k => k.Stations.Count > 0 && k.Stations[0].Body.activeSelf), "stations placed");
                yield return new WaitForSeconds(10f);

                var rig = Object.FindFirstObjectByType<PlayerRig>();
                Quiet(rig, log);
                var observer = Object.FindObjectsByType<ObserverCam>(FindObjectsSortMode.None).First(o => o.Cam && o.Cam.enabled);
                observer.enabled = false;
                var cam = observer.Cam;

                foreach (var kind in Kinds())
                    log.Add($"{tag} {kind.Kind}: {kind.Stations.Count} placed at {string.Join(" ", kind.Stations.Select(s => s.Position.ToString("F0")))}");

                foreach (var dark in new[] { false, true })
                {
                    foreach (var kind in Kinds())
                        foreach (var station in kind.Stations)
                            station.Lit.SetActive(!dark);
                    yield return Frames(3);
                    foreach (var kind in Kinds())
                    {
                        var station = kind.Stations[0];
                        Park(rig.Player, station.Body.transform.position + new Vector3(21f, -6f, 0f));
                        yield return new WaitForSeconds(1f);
                        foreach (var zoom in Zooms)
                        {
                            cam.orthographicSize = zoom;
                            Centre(cam, station.Body.transform.position);
                            yield return Frames(8);
                            Centre(cam, station.Body.transform.position);
                            yield return Frames(2);
                            Shot(cam, Path.Combine(Out, $"{tag}-{kind.Kind}-z{zoom:0}-{(dark ? "dark" : "lit")}.png"));
                        }
                    }
                }

                foreach (var kind in Kinds())
                    foreach (var station in kind.Stations)
                        station.Lit.SetActive(true);

                if (l == 0)
                {
                    var repair = Kinds().OfType<RepairStation>().Single().Stations[0];
                    Park(rig.Player, repair.Body.transform.position + new Vector3(21f, -6f, 0f));
                    cam.orthographicSize = 15f;
                    Centre(cam, repair.Body.transform.position);
                    yield return Frames(10);
                    using (CapturePacing.Locked(2))
                    using ((IDisposable)Capture.GetType().GetMethod("PrepareHud1280x720").Invoke(Capture, null))
                    using (var recording = Capture.Prepare(new CaptureRequest("results/capture", "station-swap-repair-dark", Commit, 1280, 720)))
                    {
                        yield return recording.Ready;
                        recording.Start();
                        yield return Frames(50);
                        repair.Lit.SetActive(false);
                        yield return Frames(300);
                        repair.Lit.SetActive(true);
                        yield return Frames(100);
                        Result = recording.Finish();
                    }
                }

                File.WriteAllLines(Path.Combine(Out, "log.txt"), log);
                Object.DestroyImmediate(host.gameObject);
                yield return Frames(10);
            }
        }

        private static FindStation[] Kinds() => new FindStation[]
        {
            Object.FindFirstObjectByType<RepairStation>(), Object.FindFirstObjectByType<ModStation>(),
            Object.FindFirstObjectByType<Armory>()
        }.Where(k => k).ToArray();

        private static void Quiet(PlayerRig rig, List<string> log)
        {
            foreach (var s in Object.FindObjectsByType<SectorSpawner>(FindObjectsSortMode.None)) s.enabled = false;
            var hidden = 0;
            foreach (var ship in Object.FindObjectsByType<Ship>(FindObjectsSortMode.None))
            {
                if (ship == rig.Player) continue;
                ship.gameObject.SetActive(false);
                hidden++;
            }
            var group = rig.Overlay ? rig.Overlay.GetComponent<CanvasGroup>() : null;
            if (group) group.alpha = 0f;
            log.Add($"ships hidden {hidden}, hud {(group ? "hidden" : "not found")}");
        }

        private static void Park(Ship ship, Vector3 at)
        {
            var body = ship.GetComponent<Rigidbody>();
            if (body)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = at;
            }
            ship.transform.position = at;
        }

        private static void Centre(Camera cam, Vector3 point)
        {
            var forward = cam.transform.forward;
            var t = (point.z - cam.transform.position.z) / forward.z;
            var centre = cam.transform.position + t * forward;
            cam.transform.position += point - centre;
        }

        private static void Shot(Camera cam, string path)
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
            Object.DestroyImmediate(tex);
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

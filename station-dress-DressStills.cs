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
using Substrate.Sectors.Elements.Stations.Visual;
using Substrate.Sectors.Factions;
using Substrate.Services.Locales;
using Tests.PlayMode.Recording;
using UI.Hangar.Hardware;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Scratch
{
    // Scratch capture for #1593: every station in every dress (Shoal, Veil, Bastion red hull, Bastion bone hull),
    // open, in use and dark, at orthographic size 15 and 25, on two locales, in the Bastion trial sector.
    public sealed class DressStills : CaptureScenario
    {
        private const string Out = @"C:\Users\amind\AppData\Local\Temp\claude\882050741e06a72d\7333cce7-bc83-4523-8f8d-c6070660c379\scratchpad\stills";
        private const int Sector = 2;
        private static readonly float[] Zooms = { 15f, 25f };

        private static readonly (string tag, string faction, int palette)[] Dresses =
        {
            ("shoal", "Shoal", 0), ("veil", "Veil", 0), ("bastion-red", "Bastion", 0), ("bastion-bone", "Bastion", 1),
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
                yield return WaitFor(() => Kinds().Length == 3 && Kinds().All(k => k.Stations.Count > 0 && k.Stations[0].Body.activeSelf), "stations placed");
                yield return new WaitForSeconds(6f);

                var rig = Object.FindFirstObjectByType<PlayerRig>();
                Quiet(rig, log);
                var observer = Object.FindObjectsByType<ObserverCam>(FindObjectsSortMode.None).First(o => o.Cam && o.Cam.enabled);
                observer.enabled = false;
                var cam = observer.Cam;

                // What the sector itself dressed, before the capture redresses anything.
                foreach (var kind in Kinds())
                    foreach (var station in kind.Stations)
                    {
                        var model = station.Body.GetComponentsInChildren<StationModel>(true);
                        log.Add($"{tag} sector {kind.Kind} at {station.Position:F0}: models {model.Length} {string.Join(",", model.Select(m => m.name))}; " +
                                $"rolled {Faction("Bastion").stationDress.PaletteAt(station.Position).name}");
                        station.Zone.Disarm();
                    }
                if (l == 0)
                    foreach (var kind in Kinds())
                        Shot(cam, kind.Stations[0], rig, 25f, Path.Combine(Out, $"{tag}-sector-{kind.Kind}.png"));

                foreach (var (dress, factionName, palette) in Dresses)
                {
                    var faction = Faction(factionName);
                    foreach (var kind in Kinds())
                    {
                        var station = kind.Stations[0];
                        var body = station.Body.GetComponent<FindStationBody>();
                        station.Body.SetActive(false);
                        body.SwapModel(faction.stationDress.ModelFor(kind.Kind));
                        body.Model.Wear(faction.stationDress.palettes[palette]);
                        station.Body.SetActive(true);
                        station.Lit.SetActive(true);
                    }
                    yield return Frames(3);
                    foreach (var kind in Kinds())
                    {
                        var station = kind.Stations[0];
                        foreach (var state in new[] { "open", "in-use", "dark" })
                        {
                            station.Lit.SetActive(state != "dark");
                            var at = station.Body.transform.position;
                            Park(rig.Player, state == "in-use" ? at : at + new Vector3(21f, -6f, 0f));
                            yield return new WaitForSeconds(0.6f);
                            foreach (var zoom in Zooms)
                            {
                                cam.orthographicSize = zoom;
                                Centre(cam, at);
                                yield return Frames(6);
                                Centre(cam, at);
                                yield return Frames(2);
                                Render(cam, Path.Combine(Out, $"{tag}-{dress}-{kind.Kind}-{state}-z{zoom:0}.png"));
                            }
                        }
                        station.Lit.SetActive(true);
                    }
                    log.Add($"{tag} {dress} done");
                }

                if (l == 0)
                {
                    using (CapturePacing.Locked(2))
                    using (var recording = Capture.Prepare(new CaptureRequest("results/capture", "dress-stills", Commit, 1280, 720)))
                    {
                        yield return recording.Ready;
                        recording.Start();
                        yield return Frames(4);
                        Result = recording.Finish();
                    }
                }

                File.WriteAllLines(Path.Combine(Out, "log.txt"), log);
                Object.DestroyImmediate(host.gameObject);
                yield return Frames(10);
            }
        }

        private static Faction Faction(string name) =>
            AssetDatabase.LoadAssetAtPath<Faction>($"Assets/Settings/Factions/{name}.asset");

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

        private static void Shot(Camera cam, FindStation.Station station, PlayerRig rig, float zoom, string path)
        {
            Park(rig.Player, station.Body.transform.position + new Vector3(21f, -6f, 0f));
            cam.orthographicSize = zoom;
            Centre(cam, station.Body.transform.position);
            Render(cam, path);
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

        private static void Render(Camera cam, string path)
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

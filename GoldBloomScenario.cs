#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Cameras;
using Capture;
using Capture.Recording;
using Capture.Views;
using Combat.Projectiles;
using Combat.Weapons;
using Combat.Weapons.Arsenal;
using Game;
using Ships.Command;
using Substrate;
using Substrate.Results;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Services.Locales;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using Tests.PlayMode.Recording;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Scenarios
{
    public sealed class WeaponPatternsScenario : CaptureScenario
    {
        public override bool NeedsSession => false;
        public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;

        public override IEnumerator Run()
        {
            yield return RecordWeapon("Rippers", "RipperSlug", 1);

        }

        private IEnumerator RecordWeapon(string weaponName, string projectileName, int projectilesPerEvent)
        {
            var staging = new GameObject("Needle fleck flight staging");
            staging.SetActive(false);
            var sectorTemplate = new GameObject("Needle fleck empty sector");
            sectorTemplate.SetActive(false);
            var sector = sectorTemplate.AddComponent<Sector>();
            sector.SetManifest(null, null, Array.Empty<SectorModule>());
            var locale = AssetDatabase.LoadAssetAtPath<LocaleSky>("Assets/Prefabs/Locales/IllustratedBlue.prefab");
            var run = SectorRun.Create(new SectorEntry { prefab = sector, locale = locale });
            var source = AssetDatabase.LoadAssetAtPath<GameHost>("Assets/Prefabs/Game/GameHost.prefab");
            var host = Object.Instantiate(source, staging.transform);
            host.sessionProfile = new SessionProfile { run = run, idleLocale = locale, presentation = true };
            host.hangarScreenPrefab = null;
            host.runRecordPath = Path.Combine(ResultsRoot.Folder("needle-fleck-flight-records"),
                Guid.NewGuid().ToString("N"), "runs.jsonl");
            try
            {
                host.transform.SetParent(null, false);
                yield return AsyncAssert.WaitUntil(() => host.ActiveSector, 45f, "GameHost loaded the presented sector");
                yield return null;
                var rig = host.GetComponentInChildren<PlayerRig>();
                var observer = host.GetComponentInChildren<ObserverCam>();
                var session = (Session)typeof(GameHost).GetField("session", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(host);
                Assert.IsTrue(rig.Player.gameObject.activeInHierarchy);
                rig.SetInputEnabled(false);
                rig.SwapWeapon(AssetDatabase.LoadAssetAtPath<WeaponComponent>("Assets/Prefabs/Weapons/" + weaponName + ".prefab"));
                var weapon = (WeaponBase<Laser>)rig.Player.Weapons.Primary;
                var projectile = AssetDatabase.LoadAssetAtPath<Laser>("Assets/Prefabs/Weapons/Projectiles/" + projectileName + ".prefab");
                Assert.AreSame(projectile, weapon.projectilePrefab, "Mounted weapon must fire its authored projectile.");
                rig.Player.Rigidbody.position = GamePlane.PlanePointToWorld(new Vector2(-12f, 0f));
                rig.Player.Rigidbody.rotation = GamePlane.Rotation * Quaternion.Euler(0f, 0f, -90f);
                rig.Player.Rigidbody.linearVelocity = Vector3.zero;
                rig.Player.Rigidbody.angularVelocity = Vector3.zero;
                observer.SetSubject(null, removeOldSubject: false);
                observer.SetManualCenter(Vector2.zero);
                observer.SetManualZoom(12f);
                yield return AsyncAssert.WaitUntil(() =>
                    GamePlane.WorldPointToPlane(observer.transform.position).magnitude < .01f &&
                    Mathf.Abs(observer.Cam.orthographicSize - 12f) < .001f, 15f, "authored camera settled");

                var fired = 0;
                var fireTimes = new List<float>();
                var variants = new HashSet<int>();
                var framesId = Shader.PropertyToID("_Frames");
                var phaseId = Shader.PropertyToID("_Phase");
                var phaseBlock = new MaterialPropertyBlock();
                weapon.OnFire += CountShot;
                void CountShot() { fired++; fireTimes.Add(Time.time); }
                var armed = rig.Player.Weapons.Arm(session.Projectiles);
                var command = new WeaponCommand { held = true, targetPoint = GamePlane.PlanePointToWorld(new Vector2(30f, 0f)) };
                var measured = new Dictionary<string, Func<double>>
                {
                    ["active_bolts"] = () => session.Projectiles.ActiveCount,
                    ["shots_fired"] = () => fired,
                    ["projectile_speed"] = () => projectile.LaserSpeed,
                    ["bolt_u"] = () => VisibleBolt(session, observer.Cam).x,
                    ["bolt_v"] = () => VisibleBolt(session, observer.Cam).y
                };
                using (CapturePacing.Locked())
                using (var view = (IDisposable)Capture.GetType().GetMethod("PrepareGameTopdown").Invoke(Capture, new object[] { observer }))
                using (var recording = Capture.Prepare(new CaptureRequest("results/capture", weaponName + "-firing-pattern", Commit,
                           1280, 720, measurements: measured)))
                {
                    yield return recording.Ready;
                    recording.Start();
                    for (var frame = 0; frame < 100; frame++)
                    {
                        if (frame >= 10 && frame < 80)
                        {
                            var before = session.Projectiles.ActiveCount;
                            var previousShots = fired;
                            armed.Fire(WeaponSlot.Primary, in command);
                            if (fired != previousShots)
                                Assert.AreEqual(projectilesPerEvent, session.Projectiles.ActiveCount - before);
                        }
                        yield return null;
                        if (weaponName == "Rippers") session.Projectiles.ForEachLive(item =>
                        {
                            if (((Laser)item).DistanceTraveled < 1f) return;
                            var renderer = item.transform.Find("Slug").GetComponent<Renderer>();
                            renderer.GetPropertyBlock(phaseBlock);
                            var tint = phaseBlock.GetColor("_BaseColor");
                            Assert.AreEqual(1.5f, tint.r);
                            Assert.AreEqual(1.15f, tint.g);
                            Assert.AreEqual(.45f, tint.b);
                            var count = renderer.sharedMaterial.GetFloat(framesId);
                            Assert.AreEqual(6f, count);
                            Assert.AreEqual(0f, renderer.sharedMaterial.GetFloat("_Fps"));
                            variants.Add(Mathf.FloorToInt(phaseBlock.GetFloat(phaseId) * count));
                        });
                    }
                    yield return new WaitForEndOfFrame();
                    Result = recording.Finish();
                }
                weapon.OnFire -= CountShot;
                Assert.Greater(fired, 2, "Actual weapon must fire repeatedly through the armed weapon path.");
                AssertPixels(Result, fired, projectile, weaponName, projectilesPerEvent, fireTimes.ToArray());
                if (weaponName == "Rippers") File.WriteAllText(Path.Combine(ResultsRoot.Folder("capture"), "pattern-ripper-variants-proof.json"),
                    "{\"frames\":6,\"fps\":0,\"observedFrames\":[" + string.Join(",", variants.OrderBy(value => value)) + "]}");
                yield return host.Shutdown();
            }
            finally
            {
                Object.DestroyImmediate(host.gameObject);
                Object.DestroyImmediate(run);
                Object.DestroyImmediate(sectorTemplate);
                Object.DestroyImmediate(staging);
            }
        }

        private static Vector2 VisibleBolt(Session session, Camera camera)
        {
            var point = new Vector2(-1f, -1f);
            session.Projectiles.ForEachLive(item =>
            {
                Assert.That(item, Is.InstanceOf<Laser>());
                var viewport = camera.WorldToViewportPoint(item.transform.position);
                if (viewport.z > 0f && viewport.x > .1f && viewport.x < .9f && viewport.y > .2f && viewport.y < .8f)
                    point = viewport;
            });
            return point;
        }

        private static void AssertPixels(CaptureResult result, int fired, Laser projectile, string weaponName, int projectilesPerEvent, float[] fireTimes)
        {
            var rows = File.ReadAllLines(result.FrameMetadataPath);
            var header = rows[0].Split(',');
            var uColumn = Array.IndexOf(header, "bolt_u");
            var vColumn = Array.IndexOf(header, "bolt_v");
            var activeColumn = Array.IndexOf(header, "active_bolts");
            Assert.GreaterOrEqual(uColumn, 0);
            Assert.GreaterOrEqual(vColumn, 0);
            Assert.GreaterOrEqual(activeColumn, 0);
            var baseline = new Texture2D(2, 2);
            var shot = new Texture2D(2, 2);
            try
            {
                Assert.IsTrue(baseline.LoadImage(File.ReadAllBytes(Path.Combine(result.FrameDirectory, rows[1].Split(',')[1]))));
                Assert.AreEqual(1280, baseline.width);
                Assert.AreEqual(720, baseline.height);
                Assert.Greater(baseline.GetPixels32().Distinct().Count(), 100, "The game view must contain rendered scene content.");
                var strongest = 0;
                var warmest = 0;
                var chosen = "";
                foreach (var row in rows.Skip(1))
                {
                    var fields = row.Split(',');
                    var u = float.Parse(fields[uColumn], CultureInfo.InvariantCulture);
                    var v = float.Parse(fields[vColumn], CultureInfo.InvariantCulture);
                    if (u < 0f || float.Parse(fields[activeColumn], CultureInfo.InvariantCulture) < 1f) continue;
                    Assert.IsTrue(shot.LoadImage(File.ReadAllBytes(Path.Combine(result.FrameDirectory, fields[1]))));
                    var x = Mathf.RoundToInt(u * shot.width);
                    var y = Mathf.RoundToInt(v * shot.height);
                    var brightened = 0;
                    var warm = 0;
                    for (var dy = -20; dy <= 20; dy++)
                    for (var dx = -32; dx <= 32; dx++)
                    {
                        var before = baseline.GetPixel(x + dx, y + dy);
                        var after = shot.GetPixel(x + dx, y + dy);
                        if (Mathf.Max(after.r - before.r, after.g - before.g, after.b - before.b) <= .12f) continue;
                        brightened++;
                        if (after.r > after.b * 1.15f && after.g > after.b * 1.1f) warm++;
                    }
                    if (brightened <= strongest) continue;
                    strongest = brightened;
                    warmest = warm;
                    chosen = fields[1];
                }
                Assert.Greater(strongest, 8, "An active needle fleck must brighten pixels around its projected position.");
                var materials = projectile.GetComponentsInChildren<Renderer>()
                    .SelectMany(renderer => renderer.sharedMaterials).Where(material => material)
                    .Select(material => new MaterialEvidence
                    {
                        name = material.name,
                        shader = material.shader.name,
                        emission = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black
                    }).ToArray();
                File.WriteAllText(Path.Combine(result.OutputDirectory, weaponName + "-pattern-proof.json"), JsonUtility.ToJson(new Evidence
                {
                    weapon = weaponName, boltsPerEvent = projectilesPerEvent, shotTimes = fireTimes,
                    shotsFired = fired, brightenedPixels = strongest, warmPixels = warmest, frame = chosen,
                    frameDirectory = result.FrameDirectory, projectileSpeed = projectile.LaserSpeed, materials = materials
                }, true));
                Debug.Log($"[NeedleFleckFlight] {fired} shots at {projectile.LaserSpeed} units/s, {strongest} brightened pixels, {warmest} warm pixels, frame {chosen}");
            }
            finally { Object.DestroyImmediate(baseline); Object.DestroyImmediate(shot); }
        }

        [Serializable] private sealed class MaterialEvidence { public string name; public string shader; public Color emission; }
        [Serializable] private sealed class Evidence
        {
            public string weapon;
            public int boltsPerEvent;
            public float[] shotTimes;
            public int shotsFired;
            public int brightenedPixels;
            public int warmPixels;
            public float projectileSpeed;
            public string frame;
            public string frameDirectory;
            public MaterialEvidence[] materials;
        }
    }
}
#endif

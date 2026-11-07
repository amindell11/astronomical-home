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
using Substrate.Sessions;
using Tests.PlayMode.Common;
using Tests.PlayMode.Recording;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class RipperFlightScenario : CaptureScenario
{
    public override bool NeedsSession => false;
    public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;

    public override IEnumerator Run()
    {
        var staging = new GameObject("Ripper flight staging");
        staging.SetActive(false);
        var sectorTemplate = new GameObject("Ripper flight empty sector");
        sectorTemplate.SetActive(false);
        var sector = sectorTemplate.AddComponent<Sector>();
        sector.SetManifest(null, null, Array.Empty<Substrate.Sectors.Elements.SectorModule>());
        var source = AssetDatabase.LoadAssetAtPath<GameHost>("Assets/Prefabs/Game/GameHost.prefab");
        var host = Object.Instantiate(source, staging.transform);
        host.sessionProfile = new SessionProfile
        {
            sectors = new[] { new SectorEntry { prefab = sector, config = source.sessionProfile.sectors[0].config } },
            idleLocale = source.sessionProfile.idleLocale,
            presentation = true
        };
        host.hangarScreenPrefab = null;
        host.runRecordPath = Path.Combine(ResultsRoot.Folder("ripper-flight-records"), Guid.NewGuid().ToString("N"), "runs.jsonl");
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
            rig.SwapWeapon(WeaponSlot.Primary, AssetDatabase.LoadAssetAtPath<WeaponComponent>("Assets/Prefabs/Weapons/Rippers.prefab"));
            var weapon = (Rippers)rig.Player.Weapons.Primary;
            var projectile = AssetDatabase.LoadAssetAtPath<Laser>("Assets/Prefabs/Weapons/Projectiles/RipperSlug.prefab");
            Assert.AreSame(projectile, weapon.projectilePrefab, "The mounted Rippers must fire the authored RipperSlug asset.");
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
            weapon.OnFire += CountShot;
            void CountShot() => fired++;
            var armed = rig.Player.Weapons.Arm(session.Projectiles);
            var command = new WeaponCommand { held = true, targetPoint = GamePlane.PlanePointToWorld(new Vector2(30f, 0f)) };
            var measured = new Dictionary<string, Func<double>>
            {
                ["active_bolts"] = () => session.Projectiles.ActiveCount,
                ["shots_fired"] = () => fired,
                ["bolt_u"] = () => VisibleBolt(session, observer.Cam).x,
                ["bolt_v"] = () => VisibleBolt(session, observer.Cam).y
            };
            using (CapturePacing.Locked())
            using (var view = (IDisposable)Capture.GetType().GetMethod("PrepareHud1280x720").Invoke(Capture, null))
            using (var recording = Capture.Prepare(new CaptureRequest("results/capture", "ripper-flight", Commit,
                       1280, 720, measurements: measured)))
            {
                yield return recording.Ready;
                recording.Start();
                for (var frame = 0; frame < 100; frame++)
                {
                    if (frame >= 10 && frame < 80) armed.Fire(WeaponSlot.Primary, in command);
                    yield return null;
                }
                yield return new WaitForEndOfFrame();
                Result = recording.Finish();
            }
            weapon.OnFire -= CountShot;
            Assert.Greater(fired, 2, "Actual Rippers must fire repeatedly through the armed weapon path.");
            AssertPixels(Result, fired, projectile);
            yield return host.Shutdown();
        }
        finally
        {
            Object.DestroyImmediate(host.gameObject);
            Object.DestroyImmediate(sectorTemplate);
            Object.DestroyImmediate(staging);
        }
    }

    private static Vector2 VisibleBolt(Session session, Camera camera)
    {
        var point = new Vector2(-1f, -1f);
        session.Projectiles.ForEachLive(item =>
        {
            Assert.That(item.name, Does.StartWith("RipperSlug"));
            var viewport = camera.WorldToViewportPoint(item.transform.position);
            if (viewport.z > 0f && viewport.x > .1f && viewport.x < .9f && viewport.y > .2f && viewport.y < .8f)
                point = viewport;
        });
        return point;
    }

    private static void AssertPixels(CaptureResult result, int fired, Laser projectile)
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
            Assert.Greater(baseline.GetPixels32().Distinct().Count(), 100, "The authored game view must contain rendered scene content.");
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
            Assert.Greater(strongest, 8, "An active RipperSlug must visibly brighten pixels around its projected position.");
            Assert.Greater(warmest, 5, "The visible slug must carry warm yellow pixels through game postprocessing.");
            var materials = projectile.GetComponentsInChildren<Renderer>()
                .SelectMany(renderer => renderer.sharedMaterials).Where(material => material)
                .Select(material => new MaterialEvidence
                {
                    name = material.name,
                    shader = material.shader.name,
                    emission = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black
                }).ToArray();
            File.WriteAllText(Path.Combine(result.OutputDirectory, "ripper-flight-proof.json"), JsonUtility.ToJson(new Evidence
            {
                shotsFired = fired, brightenedPixels = strongest, warmPixels = warmest, frame = chosen,
                frameDirectory = result.FrameDirectory, materials = materials
            }, true));
            Debug.Log($"[RipperFlight] {fired} actual shots, {strongest} brightened pixels, {warmest} warm pixels, evidence frame {chosen}");
        }
        finally { Object.DestroyImmediate(baseline); Object.DestroyImmediate(shot); }
    }

    [Serializable] private sealed class MaterialEvidence { public string name; public string shader; public Color emission; }
    [Serializable] private sealed class Evidence
    {
        public int shotsFired;
        public int brightenedPixels;
        public int warmPixels;
        public string frame;
        public string frameDirectory;
        public MaterialEvidence[] materials;
    }
}
#endif

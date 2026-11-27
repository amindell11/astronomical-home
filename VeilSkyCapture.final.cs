#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Ships;
using Ships.Loadout;
using Substrate;
using Substrate.Sectors;
using Substrate.Sectors.Factions;
using Substrate.Services.Locales;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Scratch
{
    // Evidence-only scratch: the three Veil ranks in a loaded sector under a locale sky, through the game camera.
    [TestFixture]
    [Category("Scratch")]
    [Category("RequiresGraphics")]
    public sealed class VeilSkyCapture : PlayModeWorldFixture
    {
        private const string SectorPath = "Assets/Prefabs/Sectors/TrialSector.prefab";
        private const string FactionPath = "Assets/Settings/Factions/Veil.asset";
        private const string CameraPath = "Assets/Prefabs/Cameras/Main Camera.prefab";
        private const float CameraDistance = 10f;

        private GameObject sessionRoot;
        private Session session;
        private GameObject cam;

        public override void TearDown()
        {
            if (cam) Object.DestroyImmediate(cam);
            DestroyTestObject(sessionRoot);
            sessionRoot = null;
            session?.Projectiles?.ReturnAllToPool();
            session = null;
            base.TearDown();
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Capture([Values("BlueCrescent", "CoralSunset")] string localeName)
        {
            LogAssert.ignoreFailingMessages = true;
            var sector = AssetDatabase.LoadAssetAtPath<Sector>(SectorPath);
            var locale = AssetDatabase.LoadAssetAtPath<LocaleSky>($"Assets/Prefabs/Locales/{localeName}.prefab");
            var faction = AssetDatabase.LoadAssetAtPath<Faction>(FactionPath);
            var trialRun = AssetDatabase.LoadAssetAtPath<SectorRun>("Assets/Settings/Game/SurvivalTrialRun.asset");
            Assert.IsNotNull(sector); Assert.IsNotNull(locale); Assert.IsNotNull(faction); Assert.IsNotNull(trialRun);

            sessionRoot = new GameObject("VeilCaptureSession");
            session = TestSession.Create(sessionRoot, new SessionProfile
            {
                run = SectorRun.Create(trialRun.Sectors[0].WithLocale(locale).WithFaction(faction)),
                presentation = true,
            });
            yield return session.Compose();
            yield return session.LoadSector();
            for (var i = 0; i < 10; i++) yield return null;
            foreach (var stray in new List<Ship>(session.Units.ActiveRegistry.ActiveShips))
            {
                Debug.Log($"[VeilCapture] despawning sector ship {stray.name}");
                session.Units.DespawnShip(stray);
            }
            yield return null;

            cam = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CameraPath));
            foreach (var behaviour in cam.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var type = behaviour.GetType().FullName ?? "";
                if (type.StartsWith("UnityEngine.Rendering")) continue;
                Object.DestroyImmediate(behaviour);
            }
            foreach (var listener in cam.GetComponentsInChildren<AudioListener>(true)) Object.DestroyImmediate(listener);
            var camera = cam.GetComponent<Camera>();
            camera.enabled = false;

            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../results/veil-before"));
            Directory.CreateDirectory(outDir);
            var log = new List<string>();

            var spots = new[] { new Vector2(-4f, 0f), new Vector2(0f, 0f), new Vector2(4.5f, 0f) };
            var middle = session.Frame.Place((spots[0] + spots[2]) * 0.5f);
            yield return Shot(camera, middle, 9f, Path.Combine(outDir, $"{localeName}-plate-9.png"));
            yield return Shot(camera, middle, 3.2f, Path.Combine(outDir, $"{localeName}-plate-3.2.png"));

            var field = session.ActiveSector ? session.ActiveSector.ObstacleField : null;
            var minionRank = faction.Minion;
            var rivalRank = faction.Rival;
            var eliteProfile = trialRun.Sectors[0].elite;
            Assert.IsNotNull(eliteProfile);
            var minion = session.Units.SpawnShip(minionRank.ship, null, 1, session.Frame.Place(spots[0]),
                GamePlane.Rotation, field, minionRank.Loadout, minionRank.scale);
            faction.Dress(minion, minionRank);
            var rival = session.Units.SpawnShip(rivalRank.ship, null, 1, session.Frame.Place(spots[1]),
                GamePlane.Rotation, field, rivalRank.Loadout, rivalRank.scale);
            faction.Dress(rival, rivalRank);
            var eliteShip = faction.EliteShip;
            var elite = session.Units.SpawnShip(eliteShip, null, 1, session.Frame.Place(spots[2]),
                GamePlane.Rotation, field, new ShipLoadout(eliteShip, eliteProfile.Primary, eliteProfile.Secondary),
                eliteProfile.Scale);
            faction.DressElite(elite);
            var ships = new (string name, Ship ship)[] { ("minion", minion), ("rival", rival), ("elite", elite) };

            for (var i = 0; i < 120; i++) yield return null;

            var groupBounds = Bounds(minion);
            groupBounds.Encapsulate(Bounds(rival));
            groupBounds.Encapsulate(Bounds(elite));
            log.Add($"{localeName} group bounds {groupBounds}");
            yield return Shot(camera, groupBounds.center, 9f, Path.Combine(outDir, $"{localeName}-group.png"));
            yield return Shot(camera, groupBounds.center, FitSize(groupBounds), Path.Combine(outDir, $"{localeName}-group-fit.png"));
            foreach (var (name, ship) in ships)
            {
                foreach (var (_, other) in ships) Show(other, other == ship);
                var b = Bounds(ship);
                log.Add($"{localeName} {name} bounds {b} scale {ship.transform.lossyScale}");
                yield return Shot(camera, b.center, 3.2f, Path.Combine(outDir, $"{localeName}-{name}.png"));
                yield return Shot(camera, b.center, FitSize(b), Path.Combine(outDir, $"{localeName}-{name}-fit.png"));
                foreach (var r in ship.VisualRig.Silhouette)
                {
                    var mats = new List<string>();
                    foreach (var m in r.sharedMaterials) mats.Add(m ? $"{m.name} ({AssetDatabase.GetAssetPath(m)})" : "null");
                    log.Add($"{localeName} {name} {ship.name} {r.name}: {string.Join(" | ", mats)}");
                }
            }
            foreach (var (_, other) in ships) Show(other, true);
            File.AppendAllLines(Path.Combine(outDir, "materials.txt"), log);
            if (session != null) yield return session.Teardown();
        }

        private static Bounds Bounds(Ship ship)
        {
            var silhouette = ship.VisualRig.Silhouette;
            var b = silhouette[0].bounds;
            foreach (var r in silhouette) b.Encapsulate(r.bounds);
            return b;
        }

        private static float FitSize(Bounds b)
        {
            var up = Mathf.Abs(Vector3.Dot(b.size, GamePlane.Forward));
            var across = Mathf.Abs(Vector3.Dot(b.size, GamePlane.Right));
            return Mathf.Max(up * 0.5f, across * 0.5f * 1080f / 1920f) * 1.15f;
        }

        private static void Show(Ship ship, bool visible)
        {
            foreach (var r in ship.VisualRig.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        }

        private static IEnumerator Shot(Camera camera, Vector3 target, float size, string path)
        {
            camera.transform.SetPositionAndRotation(target - GamePlane.Normal * CameraDistance,
                Quaternion.LookRotation(GamePlane.Normal, GamePlane.Forward));
            camera.orthographicSize = size;
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.targetTexture = rt;
            // Settle frames: a pose jump between shots otherwise reads as camera motion blur.
            for (var i = 0; i < 4; i++)
            {
                camera.Render();
                yield return null;
            }
            camera.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            camera.targetTexture = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            Debug.Log($"[VeilCapture] {path}");
        }
    }
}
#endif

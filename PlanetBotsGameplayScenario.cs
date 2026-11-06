#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cameras;
using Capture;
using Capture.Recording;
using Capture.Views;
using Game;
using NUnit.Framework;
using Ships;
using Substrate.Sectors;
using Substrate.Sessions;
using Substrate.Services.Units;
using Tests.PlayMode.Recording;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Scenarios
{
    public sealed class PlanetBotsGameplayScenario : CaptureScenario
    {
        public override bool NeedsSession => false;
        public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;

        public override IEnumerator Run()
        {
            using var pacing = CapturePacing.Locked(2);
            foreach (var id in new[] { 6, 7 })
            {
                var staging = new GameObject("Inactive bots capture staging");
                staging.SetActive(false);
                var host = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameHost>(
                    "Assets/Prefabs/Game/GameHost.prefab"), staging.transform);
                host.gameObject.SetActive(false);
                host.transform.SetParent(null, false);
                Object.DestroyImmediate(staging);
                var units = host.GetComponent<UnitService>();
                var observed = new HashSet<Ship>();
                var fired = 0;
                var hits = 0;
                Action shot = () => fired++;
                Action<Damage.AppliedDamage> hit = _ => hits++;
                void Observe(Ship ship)
                {
                    if (!observed.Add(ship)) return;
                    ship.Weapons.Fired += shot;
                    ship.Damage.OnDamaged += hit;
                }
                units.ActiveRegistry.ActiveShips.OnAdd += Observe;
                try
                {
                    var label = id == 6 ? "VerdantGiant" : "BlueCrescent";
                    host.sessionProfile = new SessionProfile
                    {
                        presentation = true,
                        idleLocale = host.sessionProfile.idleLocale,
                        sectors = new[] { new SectorEntry
                        {
                            prefab = AssetDatabase.LoadAssetAtPath<Sector>("Assets/Prefabs/Sectors/ArenaSector.prefab"),
                            config = AssetDatabase.LoadAssetAtPath<SectorSettings>(
                                "Assets/Settings/Game/Preview/Locales/" + label + ".asset")
                        } }
                    };
                    host.playerRig = null;
                    host.hangarScreenPrefab = null;
                    host.musicPrefab = null;
                    var recordFolder = Path.GetFullPath("results/capture/bots-records");
                    Directory.CreateDirectory(recordFolder);
                    host.runRecordPath = Path.Combine(recordFolder, Guid.NewGuid().ToString("N") + ".jsonl");
                    host.gameObject.SetActive(true);
                    var deadline = Time.realtimeSinceStartup + 30;
                    while ((!host.ActiveSector || units.ActiveRegistry.ActiveShips.Count < 4) && Time.realtimeSinceStartup < deadline)
                        yield return null;
                    Assert.IsTrue(host.ActiveSector, "The real GameHost loaded its arena.");
                    Assert.That(units.ActiveRegistry.ActiveShips.Count, Is.GreaterThanOrEqualTo(4));
                    var observer = host.GetComponentInChildren<ObserverCam>();
                    Assert.IsTrue(observer);
                    Ship followed = null;
                    void FollowBot()
                    {
                        if (followed && followed.gameObject.activeInHierarchy) return;
                        followed = units.ActiveRegistry.ActiveShips.Where(s => s && s.gameObject.activeInHierarchy)
                            .OrderBy(s => (s.transform.position - observer.transform.position).sqrMagnitude).FirstOrDefault();
                        if (followed) observer.SetSubject(followed.transform, followed.Body, removeOldSubject: false);
                    }
                    FollowBot();
                    using (var view = (IDisposable)Capture.GetType().GetMethod("PrepareGameTopdown")
                               .Invoke(Capture, new object[] { observer }))
                    {
                        var settleUntil = Time.time + 3;
                        while (Time.time < settleUntil) { FollowBot(); yield return null; }
                        var seconds = 0f;
                        using var recording = Capture.Prepare(new CaptureRequest("results/capture",
                            $"Locale_{id}-bots-gameplay-A", Commit, 1280, 720,
                            measurements: new Dictionary<string, Func<double>>
                            {
                                ["game_seconds"] = () => seconds,
                                ["camera_x"] = () => observer.transform.position.x,
                                ["camera_y"] = () => observer.transform.position.y,
                                ["camera_half_height"] = () => observer.Cam.orthographicSize,
                                ["live_ships"] = () => units.ActiveRegistry.ActiveShips.Count,
                                ["shots"] = () => fired,
                                ["damage_events"] = () => hits
                            }));
                        yield return recording.Ready;
                        var start = Time.time;
                        var origin = observer.transform.position;
                        var travel = 0f;
                        var initialShots = fired;
                        var initialHits = hits;
                        recording.Start();
                        while (seconds < 40)
                        {
                            FollowBot();
                            seconds = Time.time - start;
                            travel = Mathf.Max(travel, Vector2.Distance(origin, observer.transform.position));
                            yield return null;
                        }
                        Result = recording.Finish();
                        Debug.Log($"BOTS_GAMEPLAY=Locale_{id} FRAMES={Result.FrameDirectory} SHOTS={fired - initialShots} HITS={hits - initialHits} CAMERA_TRAVEL={travel}");
                        Assert.That(Result.FrameCount, Is.GreaterThan(900));
                        Assert.That(fired - initialShots, Is.GreaterThan(10), "The bots must fire during the footage.");
                        Assert.That(hits - initialHits, Is.GreaterThan(0), "The footage must contain actual damage.");
                        Assert.That(travel, Is.GreaterThan(5), "The production camera must track natural flight.");
                    }
                    yield return host.Shutdown();
                }
                finally
                {
                    units.ActiveRegistry.ActiveShips.OnAdd -= Observe;
                    foreach (var ship in observed)
                    {
                        if (!ship) continue;
                        ship.Weapons.Fired -= shot;
                        ship.Damage.OnDamaged -= hit;
                    }
                    units.Clear();
                    Object.DestroyImmediate(host.gameObject);
                }
                yield return null;
            }
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Capture.Recording;
using Capture.Views;
using Damage;
using Game;
using Game.Preview;
using NUnit.Framework;
using Ships;
using Ships.Registry;
using Substrate.Sectors.Elements;
using Substrate.Sectors.Elements.Phases;
using Tests.PlayMode.Recording;
using UI.Hangar;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tests.PlayMode.Scenarios
{
    public sealed class CanopyEventsScenario : CaptureScenario
    {
        public override bool NeedsSession => false;
        public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;

        public override IEnumerator Run()
        {
            var root = new GameObject("Canopy event capture");
            root.SetActive(false);
            var host = root.AddComponent<PreviewHost>();
            host.gameHostPrefab = AssetDatabase.LoadAssetAtPath<GameHost>("Assets/Prefabs/Game/GameHost.prefab");
            host.sessionProfile = host.gameHostPrefab.sessionProfile;
            host.mode = PreviewMode.Playtest;
            host.playtestSetup = PreviewPlaytestSetup.Combat;
            host.recapFeel = host.gameHostPrefab.recapFeel;
            host.jumpFeel = host.gameHostPrefab.jumpFeel;
            host.canopyFeel = UnityEngine.Object.Instantiate(host.gameHostPrefab.playerRig.AuthoredCanopyProfile);
            var profile = host.canopyFeel;
            var savedTimeScale = Time.timeScale;
            var renderSettings = GraphicsSettings.GetRenderPipelineSettings<RenderGraphSettings>();
            var savedCompatibility = renderSettings.enableRenderCompatibilityMode;
            PlayerRig rig = null;
            var stage = 0;
            try
            {
                root.SetActive(true);
                var deadline = Time.realtimeSinceStartup + 60f;
                var launched = false;
                while (!rig || !rig.Player || !rig.Player.gameObject.activeInHierarchy || rig.Tally.SecondsSurvived < 2f)
                {
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Combat preview did not reach flight.");
                    if (host.Playtest)
                    {
                        rig = host.Playtest.GetComponentInChildren<PlayerRig>();
                        var hangar = host.Playtest.GetComponentInChildren<HangarScreen>();
                        if (!launched && hangar && hangar.launchButton)
                        {
                            hangar.launchButton.onClick.Invoke();
                            launched = true;
                        }
                    }
                    yield return null;
                }
                Time.timeScale = 0f;
                var player = rig.Player;
                player.Damage.SetInvulnerability(0f);
                player.Damage.Health.Reset();
                player.Damage.Shield.Reset();
                var director = host.Playtest.ActiveSector.GetComponentInChildren<WaveDirector>();
                Assert.That(director, Is.Not.Null);
                director.enabled = false;
                var directorData = new SerializedObject(director);
                var phases = directorData.FindProperty("phases");
                var lull = directorData.FindProperty("lullSeconds").floatValue;
                var nextPhase = phases.GetArrayElementAtIndex(0).FindPropertyRelative("seconds").floatValue + lull + .1f;
                var completion = lull * (phases.arraySize - 1) + 1f;
                for (var i = 0; i < phases.arraySize; i++)
                    completion += phases.GetArrayElementAtIndex(i).FindPropertyRelative("seconds").floatValue;
                director.BeginSchedule(0f);
                var enemy = UnityEngine.Object.FindObjectsByType<Ship>(FindObjectsSortMode.None)
                    .First(ship => ship != player && ship.gameObject.activeInHierarchy);
                enemy.transform.position = player.transform.position + new Vector3(14f, 5f, 0f);
                enemy.Lock.RaiseReleased();
                foreach (var volume in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
                    if (volume.profile.TryGet<FilmGrain>(out var grain)) grain.intensity.Override(0f);
                using (var view = (IDisposable)Capture.GetType().GetMethod("PrepareHud1280x720").Invoke(Capture, null))
                {
                    renderSettings.enableRenderCompatibilityMode = false;
                    using var recording = Capture.Prepare(new CaptureRequest("results/capture", "canopy-events", Commit,
                        1280, 720, measurements: new Dictionary<string, Func<double>>
                        {
                            ["stage"] = () => stage,
                            ["state"] = () => (int)rig.Projection.ActiveState,
                            ["hit_age"] = () => rig.Projection.HitAge,
                            ["transition_age"] = () => rig.Projection.TransitionAge,
                            ["hull"] = () => player.Damage.Health.Pct,
                            ["effects"] = () => rig.Projection.EffectsEnabled ? 1 : 0
                        }));
                    yield return recording.Ready;
                    recording.Start();
                    var started = Time.unscaledTime;
                    while (Time.unscaledTime - started < 12f)
                    {
                        var elapsed = Time.unscaledTime - started;
                        if (stage == 0 && elapsed >= 1f)
                        {
                            stage = 1;
                            player.Damage.TakeDamage(new DamageInfo(4f, DamageKind.Laser, ShipId.Invalid,
                                0f, Vector3.zero, player.transform.position));
                        }
                        if (stage == 1 && elapsed >= 2.4f)
                        {
                            stage = 2;
                            player.Damage.Health.SetFraction(.2f);
                        }
                        if (stage == 2 && elapsed >= 5.1f)
                        {
                            stage = 3;
                            player.Damage.Health.Reset();
                            director.Advance(nextPhase);
                        }
                        if (stage == 3 && elapsed >= 7.2f)
                        {
                            stage = 4;
                            director.Advance(completion);
                        }
                        if (stage == 4 && elapsed >= 9.3f)
                        {
                            stage = 5;
                            enemy.Lock.RaiseProgress(.65f);
                        }
                        if (stage == 5 && elapsed >= 10.8f)
                        {
                            stage = 6;
                            profile.personalityEnabled = false;
                        }
                        yield return null;
                    }
                    yield return new WaitForEndOfFrame();
                    Result = recording.Finish();
                    Assert.That(Result.FrameCount, Is.GreaterThan(60));
                }
                Time.timeScale = savedTimeScale;
                yield return host.Shutdown();
            }
            finally
            {
                Time.timeScale = savedTimeScale;
                renderSettings.enableRenderCompatibilityMode = savedCompatibility;
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }
    }
}
#endif

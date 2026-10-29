using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cameras;
using Capture.GameView;
using Capture.Recording;
using Combat.Weapons;
using Combat.Weapons.Conditions;
using Game;
using Game.Player;
using Game.Preview;
using Ships.Command;
using UI;
using UI.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

public static class CaptureConcussionShared
{
    private const string Root = "D:/amind/git/agent-1/scratch/crosshair/concussion-shared";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    public static string Main()
    {
        var host = Object.FindFirstObjectByType<PreviewHost>();
        host.StartCoroutine(Record(host));
        return "Recording real weapon firing and recovery through the mounted weapon actuator";
    }

    private static IEnumerator Record(PreviewHost host)
    {
        var savedRate = Application.targetFrameRate;
        var savedVsync = QualitySettings.vSyncCount;
        Application.targetFrameRate = 20;
        QualitySettings.vSyncCount = 0;
        EditorApplication.isPaused = false;
        Time.timeScale = 1f;
        try
        {
            foreach (var name in File.ReadAllLines(Root + "/weapons.txt"))
            {
                File.WriteAllText(Root + "/status.txt", "Recording " + name);
                while (!host.IsReady) yield return null;
                host.RestartPlaytest();
                while (!host.IsReady) yield return null;
                var rig = host.Playtest.GetComponentInChildren<PlayerRig>();
                while (!rig.Player.gameObject.activeInHierarchy) yield return null;
                rig.Loadout.PrimaryWeapon = AssetDatabase.LoadAssetAtPath<WeaponComponent>("Assets/Prefabs/Weapons/" + name + ".prefab");
                rig.Loadout.SecondaryWeapon = null;
                rig.ApplyLoadout();
                rig.Player.Damage.SetInvulnerability(120f);
                foreach (var ai in Object.FindObjectsByType<AI.AICommander>(FindObjectsSortMode.None)) ai.enabled = false;
                var commander = (PlayerCommander)rig.Player.Commander;
                var weapons = (IWeapons)typeof(PlayerCommander).GetField("weapons", Private).GetValue(commander);
                rig.SetInputEnabled(false);
                yield return new WaitForSeconds(.3f);
                var body = rig.Player.GetComponent<Rigidbody>();
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.rotation = Quaternion.Euler(0f, 0f, -40f);
                var observer = host.Playtest.GetComponentInChildren<ObserverCam>();
                observer.enabled = false;
                observer.transform.position = rig.Player.transform.position + new Vector3(-4f, -3f, -50f);
                observer.Cam.orthographicSize = 18f;
                if (name == "Grenades")
                {
                    observer.SetLockCameraToSubject(true);
                    observer.SetManualZoom(18f);
                    observer.SetSubject(rig.Player.transform);
                    observer.enabled = true;
                    yield return new WaitForSeconds(.75f);
                }
                yield return CaptureOne(rig, weapons, observer, name);
            }
            File.WriteAllText(Root + "/status.txt", "Complete");
        }
        finally
        {
            Application.targetFrameRate = savedRate;
            QualitySettings.vSyncCount = savedVsync;
            EditorApplication.isPaused = true;
        }
    }

    private static IEnumerator CaptureOne(PlayerRig rig, IWeapons weapons, ObserverCam observer, string name)
    {
        var weapon = rig.Player.Weapons.Primary;
        weapon.Reset();
        var heat = weapon.GetComponent<Heat>();
        var rounds = weapon.GetComponent<Rounds>();
        var charge = weapon.GetComponent<ChargeTime>();
        var cooldown = weapon.GetComponent<Cooldown>();
        var crosshair = rig.Overlay.GetComponentInChildren<AimCrosshair>();
        var hud = rig.Overlay.GetComponentInChildren<WeaponResourceHud>();
        var source = hud.transform.Find("PrimaryResources");
        var images = source.GetComponentsInChildren<UnityEngine.UI.Image>();
        var legacyAmmo = rig.Overlay.GetComponentsInChildren<AmmoCounterUI>().Length;
        var separateReload = images.Any(image => image.name.Contains("Reload"));
        if (legacyAmmo != 0 || separateReload)
            throw new InvalidOperationException(name + " has a separate ammo/reload widget.");
        var target = rig.Player.transform.position + Quaternion.Euler(0f, 0f, -40f) * Vector3.up * 12f;
        var controls = (PlayerControls)typeof(AimCrosshair).GetField("controls", Private).GetValue(crosshair);
        var mouse = InputSystem.AddDevice<Mouse>();
        var savedInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        controls.asset.devices = new InputDevice[] { mouse };
        Action stageAim = () =>
        {
            if (InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            var aim = (Vector2)observer.Cam.WorldToScreenPoint(target);
            aim.x += Time.frameCount % 2 * .2f;
            InputState.Change(mouse, new MouseState { position = aim });
        };
        InputSystem.onAfterUpdate += stageAim;
        var shots = new List<double>();
        var started = Time.timeAsDouble;
        Action onShot = () => shots.Add(Time.timeAsDouble - started);
        weapon.OnFire += onShot;
        var capture = ScriptableObject.CreateInstance<GameViewEpisodeCapture>();
        var measurements = new Dictionary<string, Func<double>>
        {
            ["shots"] = () => shots.Count,
            ["ammo"] = () => rounds ? rounds.AmmoCount : -1,
            ["reload"] = () => rounds ? rounds.ReloadProgress : -1,
            ["heat"] = () => heat ? heat.HeatPct : -1,
            ["overheated"] = () => heat && heat.Overheated ? 1 : 0,
            ["charge"] = () => charge ? charge.ChargePct : -1,
            ["hud_fill"] = () => images.Where(image => image.name == "Fill" || image.name.StartsWith("Round") && !image.name.StartsWith("RoundTrack")).Sum(image => image.fillAmount),
        };
        try
        {
            using (capture.PrepareHud1280x720())
            using (var recording = capture.Prepare(new CaptureRequest(Root, "weapon-" + name.ToLowerInvariant(),
                File.ReadAllText(Root + "/commit.txt").Trim(), 1280, 720, measurements: measurements)))
            {
                yield return recording.Ready;
                yield return null;
                started = Time.timeAsDouble;
                recording.Start();
                var spent = false;
                var wasHeld = false;
                var nextPress = .65;
                var completeAt = -1.0;
                var pressInterval = Math.Max(1.15, cooldown.SecondsBetweenShots + .1);
                while (Time.timeAsDouble - started < 42.0)
                {
                    yield return new WaitForFixedUpdate();
                    var elapsed = Time.timeAsDouble - started;
                    if (heat && heat.Overheated || rounds && rounds.AmmoCount == 0 || charge && shots.Count >= 1)
                        spent = true;
                    var held = false;
                    if (!spent && elapsed >= .65)
                    {
                        if (weapon.AutoFire || charge) held = true;
                        else if (elapsed >= nextPress)
                        {
                            held = true;
                            nextPress = elapsed + pressInterval;
                        }
                    }
                    weapons.Fire(WeaponSlot.Primary, new WeaponCommand { held = held, pressed = held && !wasHeld, targetPoint = target });
                    wasHeld = held;
                    var recovered = spent && (heat ? !heat.Overheated && heat.HeatPct == 0f
                        : rounds ? !rounds.IsReloading && rounds.AmmoCount == rounds.MaxAmmo
                        : charge.ChargePct == 0f && cooldown.CanFire());
                    if (recovered && completeAt < 0) completeAt = elapsed + 1.2;
                    if (completeAt >= 0 && elapsed >= completeAt) break;
                }
                weapons.Fire(WeaponSlot.Primary, new WeaponCommand { targetPoint = target });
                if (completeAt < 0 || shots.Count == 0)
                    throw new InvalidOperationException(name + " did not complete a firing/recovery cycle.");
                yield return null;
                var result = recording.Finish();
                File.WriteAllText(Root + "/" + name + "-capture.json", Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    weapon = name, commit = File.ReadAllText(Root + "/commit.txt").Trim(), result.FrameDirectory,
                    result.FrameMetadataPath, result.ProvenancePath, result.FrameCount, shotTimes = shots,
                    maxAmmo = rounds ? rounds.MaxAmmo : -1, reloadSeconds = rounds ? rounds.ReloadTime : -1,
                    refill = rounds ? rounds.Refill.ToString() : "none", finalAmmo = rounds ? rounds.AmmoCount : -1,
                    finalHeat = heat ? heat.HeatPct : -1, finalCharge = charge ? charge.ChargePct : -1,
                    legacyAmmoWidgets = legacyAmmo, separateReloadWidgets = separateReload,
                    hudImages = images.Select(image => image.name).ToArray(), playbackSpeed = 1
                }));
            }
        }
        finally
        {
            weapon.OnFire -= onShot;
            InputSystem.onAfterUpdate -= stageAim;
            controls.asset.devices = null;
            InputSystem.settings.editorInputBehaviorInPlayMode = savedInput;
            InputSystem.RemoveDevice(mouse);
            Object.DestroyImmediate(capture);
        }
    }
}

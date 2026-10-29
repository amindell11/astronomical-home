using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cameras;
using Capture.GameView;
using Capture.Recording;
using Combat.Weapons.Conditions;
using Game;
using Game.Preview;
using UI.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class CaptureBuiltCrosshair
{
    private const string Root = "D:/amind/git/agent-1/scratch/crosshair";
    public static string Main()
    {
        var host = Object.FindFirstObjectByType<PreviewHost>();
        host.StartCoroutine(Record(host));
        return "Recording the production crosshair and live resource bindings";
    }

    private static IEnumerator Record(PreviewHost host)
    {
        EditorApplication.isPaused = false;
        Time.timeScale = 1f;
        while (!host.IsReady) yield return null;
        host.RestartPlaytest();
        while (!host.IsReady) yield return null;
        var rig = host.Playtest.GetComponentInChildren<PlayerRig>();
        while (!rig.Player.gameObject.activeInHierarchy) yield return null;
        rig.Player.Damage.SetInvulnerability(1000);
        yield return new WaitForSeconds(2f);
        Time.timeScale = 0f;
        var observer = host.Playtest.GetComponentInChildren<ObserverCam>();
        observer.enabled = false;
        observer.transform.position = new Vector3(-4f, -1f, -50f);
        observer.Cam.orthographicSize = 18f;
        rig.Player.Keystone.Step(rig.Player.Kinematics, 1f, 0f, out _);
        rig.Player.Keystone.Step(rig.Player.Kinematics, 0f, rig.Player.Keystone.Cooldown * .5f, out _);
        var source = rig.Overlay.GetComponentInChildren<WeaponReadoutBuilder>();
        var heat = (Heat)source.FirstReadout<IHeatReadout>();
        var rounds = (Rounds)source.FirstReadout<IAmmoReadout>();
        var crosshair = rig.Overlay.GetComponentInChildren<AimCrosshair>();
        var weaponHud = rig.Overlay.GetComponentInChildren<WeaponResourceHud>();
        var mouse = InputSystem.AddDevice<Mouse>();
        var inputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var crosshairControls = (Game.Player.PlayerControls)typeof(AimCrosshair).GetField("controls", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(crosshair);
        crosshairControls.asset.devices = new InputDevice[] { mouse };
        var stagedMouse = new MouseState { position = new Vector2(880f, 430f) };
        System.Action stageInput = () => {
            if (InputState.currentUpdateType == InputUpdateType.Dynamic) { var sample = stagedMouse; sample.position += new Vector2((Time.frameCount % 2) * .2f, 0f); InputState.Change(mouse, sample); }
        };
        InputSystem.onAfterUpdate += stageInput;
        var capture = ScriptableObject.CreateInstance<GameViewEpisodeCapture>();
        var rows = new List<object>();
        var names = new[] { "persistent-idle", "persistent-reloading", "persistent-overheated", "persistent-1080p" };
        
        try
        {
            using (capture.PrepareHud1280x720())
            {
                for (var index = 0; index < names.Length; index++)
                {
                    heat.Reset();
                    for (var i = 0; i < (index == 2 ? 20 : 6); i++) heat.ProcessFire();
                    rounds.Reset();rounds.ProcessFire();
                    if (index == 1) { rounds.ProcessFire();rounds.Tick(rounds.ReloadTime * .5f); }
                    var width = index == 3 ? 1920 : 1280;
                    var height = index == 3 ? 1080 : 720;
                    var state = new MouseState { position = index == 3 ? new Vector2(600f, 700f) : new Vector2(880f, 430f) };
                    stagedMouse = new MouseState { position = state.position };
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = state.position });
                    InputSystem.Update();
                    yield return new WaitForSecondsRealtime(.45f);
                    stagedMouse = state;
                    InputSystem.QueueStateEvent(mouse, state);InputSystem.Update();
                    using (var recording = capture.Prepare(new CaptureRequest(Root, names[index],
                        File.ReadAllText(Root + "/capture-commit.txt").Trim(), width, height)))
                    {
                        yield return recording.Ready;
                        stagedMouse = new MouseState { position = Vector2.zero };
                        InputSystem.QueueStateEvent(mouse, stagedMouse);
                        yield return null;
                        stagedMouse = state;
                        for (var frame = 0; frame < 5; frame++) { InputSystem.QueueStateEvent(mouse, state); yield return null; }
                        recording.Start();
                        for (var frame = 0; frame < 5; frame++) { InputSystem.QueueStateEvent(mouse, state); yield return null; }
                        rows.Add(new { name = names[index], heat = heat.HeatPct, heat.Overheated,
                            rounds.AmmoCount, rounds.MaxAmmo, rounds.IsReloading, rounds.ReloadProgress,
                            keystoneFill = 1f - rig.Player.BoostCooldownPct,
                            primary = weaponHud.transform.Find("PrimaryResources").gameObject.activeSelf,
                            secondary = weaponHud.transform.Find("SecondaryResources").gameObject.activeSelf,
                            rawAim = crosshairControls.Flight.AimPoint.ReadValue<Vector2>().ToString(),
                            deviceAim = mouse.position.ReadValue().ToString(),
                            aimPhase = crosshairControls.Flight.AimPoint.phase.ToString(),
                            aimEnabled = crosshairControls.Flight.AimPoint.enabled,
                            aim = RectTransformUtility.WorldToScreenPoint(rig.Overlay.WorldCamera, crosshair.transform.position).ToString(),
                            canvasScale = crosshair.GetComponentInParent<Canvas>().rootCanvas.scaleFactor,
                            markerScale = crosshair.transform.localScale.x,
                            cursorVisible = Cursor.visible });
                        var result = recording.Finish();
                        var images = Directory.GetFiles(result.FrameDirectory, "*.png");System.Array.Sort(images);
                        File.Copy(images[images.Length - 1], Root + "/" + names[index] + ".png", true);
                    }
                }
            }
            File.WriteAllText(Root + "/persistent-state.json", Newtonsoft.Json.JsonConvert.SerializeObject(rows));
        }
        finally
        {
            InputSystem.onAfterUpdate -= stageInput;
            crosshairControls.asset.devices = null;
                InputSystem.settings.editorInputBehaviorInPlayMode = inputBehavior;
            InputSystem.RemoveDevice(mouse);
            Object.DestroyImmediate(capture);
            EditorApplication.isPaused = true;
        }
    }
}
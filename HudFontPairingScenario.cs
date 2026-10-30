#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Capture.Recording;
using Game;
using Game.Preview;
using NUnit.Framework;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sessions;
using Tests.PlayMode.Recording;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class HudFontPairingScenario : CaptureScenario
{
    private const string Output = "D:/amind/git/agent-2/results/hud-font-pairing";
    private PreviewHost host;
    private PlayerRig rig;
    private WaveDirector director;
    public override bool NeedsSession => false;

    public override IEnumerator Run()
    {
        Directory.CreateDirectory(Output);
        WireFonts();
        var root = new GameObject("Font pairing playtest");
        root.SetActive(false);
        host = root.AddComponent<PreviewHost>();
        host.gameHostPrefab = AssetDatabase.LoadAssetAtPath<GameHost>("Assets/Prefabs/Game/GameHost.prefab");
        host.sessionProfile = new SessionProfile { presentation = true };
        host.mode = PreviewMode.Playtest;
        host.playtestSetup = PreviewPlaytestSetup.Combat;
        host.recapFeel = host.gameHostPrefab.recapFeel;
        host.jumpFeel = host.gameHostPrefab.jumpFeel;
        root.SetActive(true);
        yield return new WaitUntil(() => Object.FindFirstObjectByType<UI.Hangar.HangarScreen>());
        var hangar = Object.FindFirstObjectByType<UI.Hangar.HangarScreen>();
        ((Button)typeof(UI.Hangar.HangarScreen).GetField("launchButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hangar)).onClick.Invoke();
        yield return new WaitUntil(() => Object.FindFirstObjectByType<WaveDirector>());
        yield return new WaitForSecondsRealtime(1.5f);
        Time.timeScale = 0;
        rig = Object.FindFirstObjectByType<PlayerRig>();
        director = Object.FindFirstObjectByType<WaveDirector>();
        var assembly = Capture.GetType().Assembly;
        var presentation = assembly.GetType("Capture.GameView.Views.PresentationSettings");
        var snapshot = Activator.CreateInstance(assembly.GetType("Capture.GameView.Recovery.CaptureRecoveryState"));
        presentation.GetMethod("Snapshot").Invoke(null, new[] { snapshot });
        presentation.GetMethod("HideDebugOverlay").Invoke(null, null);
        FramePlayer();
        yield return FilmState("running-1080", 1920, 1080);
        yield return FilmState("running-720", 1280, 720);
        yield return FilmState("final-720", 1280, 720, () => SeedElapsed(99.5f));
        yield return FilmState("notification-1080", 1920, 1080, () => SeedElapsed(100f));
        yield return new WaitForSecondsRealtime(2.3f);
        yield return FilmState("open-1080", 1920, 1080);
        var zone = Object.FindFirstObjectByType<Objectives.ExtractionZone>();
        rig.Player.Body.position = zone.transform.position;
        rig.Player.Body.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
        Time.timeScale = 1;
        yield return new WaitForSecondsRealtime(.25f);
        Time.timeScale = 0;
        FramePlayer();
        yield return SceneManager.LoadSceneAsync("Locale_1", LoadSceneMode.Additive);
        SceneManager.SetActiveScene(SceneManager.GetSceneByName("Locale_1"));
        yield return FilmState("jump-dark-720", 1280, 720);
        presentation.GetMethod("Restore").Invoke(null, new[] { snapshot });
        yield return host.Shutdown();
        Object.DestroyImmediate(root);
        Time.timeScale = 1;
    }

    private static void WireFonts()
    {
        var orbitron = AssetDatabase.LoadAssetAtPath<Font>("Assets/Visuals/Ui/Fonts/Orbitron-Medium.ttf");
        var rajdhani = AssetDatabase.LoadAssetAtPath<Font>("Assets/Visuals/Ui/Fonts/Rajdhani-Medium.ttf");
        Assert.IsTrue(orbitron && rajdhani);
        foreach (var font in new[] { orbitron, rajdhani })
        {
            font.RequestCharactersInTexture("0123456789:EXTRACTION OPEN", 24, FontStyle.Normal);
            Assert.IsTrue(font.GetCharacterInfo('8', out var glyph, 24, FontStyle.Normal));
            Assert.Greater(glyph.advance, 0);
        }
        var path = "Assets/Prefabs/UI/UIOverlay.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        var fields = new SerializedObject(root.GetComponent<UI.Overlay>());
        fields.FindProperty("labelFont").objectReferenceValue = rajdhani;
        fields.FindProperty("readoutFont").objectReferenceValue = orbitron;
        fields.FindProperty("timerFont").objectReferenceValue = orbitron;
        fields.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        path = "Assets/Prefabs/UI/Readouts/WeaponReadoutPanel.prefab";
        root = PrefabUtility.LoadPrefabContents(path);
        root.GetComponentInChildren<Text>(true).font = rajdhani;
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();
        File.WriteAllLines(Path.Combine(Output, "unity-import.txt"), new[] { orbitron, rajdhani }.Select(f => f.name + " dynamic=" + f.dynamic + " families=" + string.Join("/", f.fontNames)));
    }

    private void FramePlayer() => Object.FindFirstObjectByType<Cameras.ObserverCam>().SetManualCenter(Substrate.GamePlane.WorldPointToPlane(rig.Player.Body.position));

    private void SeedElapsed(float seconds) => typeof(WaveDirector).GetField("startTime", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(director, Time.time - seconds);

    private IEnumerator FilmState(string name, int width, int height, Action stage = null)
    {
        using var recording = Capture.Prepare(new CaptureRequest(Output, name, Commit, width, height));
        yield return recording.Ready;
        stage?.Invoke();
        yield return null;
        if (name == "notification-1080") yield return new WaitForSecondsRealtime(.8f);
        recording.Start();
        for (var i = 0; i < 3; i++) yield return new WaitForEndOfFrame();
        Result = recording.Finish();
        var frame = Directory.GetFiles(Result.FrameDirectory, "*.png").OrderBy(x => x).First();
        File.Copy(frame, Path.Combine(Output, name + ".png"), true);
        File.WriteAllLines(Path.Combine(Output, name + "-fonts.txt"), Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(t => t.enabled && !string.IsNullOrEmpty(t.text)).Select(t => t.name + " | " + t.text + " | " + t.font.name + " | " + t.fontSize + " | " + t.fontStyle));
        Assert.AreEqual(width, Screen.width);
        Assert.AreEqual(height, Screen.height);
    }
}
#endif

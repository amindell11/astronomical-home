#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using Cameras;
using Capture;
using Game;
using Game.Runs;
using Substrate;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using UI.Screens;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Pixel-parity baseline for #1152: the real GameHost on main over a bare two-sector profile, the
/// observer camera pointed at empty space, filmed at the recap's hold states at two resolutions.
/// States: extracted card, victory banner, victory card, defeat banner, defeat card. The run data is
/// pinned by waiting on the run tally (6 s per sector, 15 s total) so the card's numbers are known.
/// </summary>
public sealed class RecapBaselineScenario : CaptureScenario
{
    private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
    private const string ObserverCamPrefabPath = "Assets/Prefabs/Cameras/Main Camera.prefab";
    private static readonly Vector2 EmptySpace = new(4000f, 4000f);
    private static readonly (int width, int height)[] Sizes = { (1280, 720), (1920, 1080) };

    private GameObject hostGo;
    private GameHost host;
    private PlayerRig rig;
    private ObserverCam observer;
    private SectorSettings bareConfig;
    private string stamp;

    public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;

    public override CaptureConfig Config => new()
    {
        clipName = "baseline", width = 1280, height = 720, everyFixedSteps = 5,
        configureView = (camera, light) => { camera.enabled = false; light.enabled = false; }
    };

    public override IEnumerator Run()
    {
        stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var input = new InputTestFixture();
        input.Setup();
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var dir = Path.Combine(Path.GetTempPath(), "recap-baseline-" + Guid.NewGuid().ToString("N"));
        try
        {
            BootHost(Path.Combine(dir, "run-records.jsonl"));
            yield return WaitFor(() => rig = hostGo.GetComponentInChildren<PlayerRig>(), "rig built");
            yield return WaitFor(() => rig.Tally.SecondsSurvived > 0f && InSector("SectorA"), "first sector");
            observer = hostGo.GetComponentInChildren<ObserverCam>();
            yield return LookAtEmptySpace();

            // Sector A: extracted at tally 6 → the EXTRACTED card reads 0:06.
            yield return WaitFor(() => rig.Tally.SecondsSurvived >= 6f, "six seconds survived");
            host.ActiveSector.GetComponent<ExtractOnCueModule>().Extract();
            yield return WaitFor(() => Screen(), "extracted card");
            yield return FilmStates("extracted");
            Continue();

            yield return WaitFor(() => InSector("SectorB"), "second sector");
            yield return WaitFor(() => rig.Tally.SecondsSurvived >= 15f, "fifteen seconds survived");
            host.ActiveSector.GetComponent<ExtractOnCueModule>().Extract();
            yield return WaitFor(() => Banner() && Banner().alpha >= 1f && Prompt(), "victory banner held");
            yield return FilmStates("victory-banner");
            yield return PressKey(input, keyboard);
            yield return WaitFor(() => Details().interactable, "victory card held");
            yield return FilmStates("victory-card");
            Continue();

            // The next run: die at tally 6 so the board shows the victory row above this run.
            yield return WaitFor(() => InSector("SectorA") && rig.Player.gameObject.activeInHierarchy
                                       && rig.Tally.SecondsSurvived > 0f, "next run");
            yield return LookAtEmptySpace();
            yield return WaitFor(() => rig.Tally.SecondsSurvived >= 6f, "six seconds survived again");
            TestDamage.Kill(rig.Player);
            yield return WaitFor(() => Banner() && Banner().alpha >= 1f && Prompt(), "defeat banner held");
            yield return FilmStates("defeat-banner");
            yield return PressKey(input, keyboard);
            yield return WaitFor(() => Details().interactable, "defeat card held");
            yield return FilmStates("defeat-card");
        }
        finally
        {
            if (hostGo) UnityEngine.Object.DestroyImmediate(hostGo);
            if (bareConfig) UnityEngine.Object.DestroyImmediate(bareConfig);
            input.TearDown();
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    private void BootHost(string recordPath)
    {
        // A locale-less config: no sky, so the frame holds only the camera's clear colour and the UI.
        bareConfig = ScriptableObject.CreateInstance<SectorSettings>();
        var templates = new[] { NewSectorTemplate("SectorA"), NewSectorTemplate("SectorB") };
        hostGo = new GameObject("BaselineHost");
        hostGo.SetActive(false);
        foreach (var template in templates) template.transform.SetParent(hostGo.transform);
        host = hostGo.AddComponent<GameHost>();
        host.sessionProfile = new SessionProfile
        {
            sectors = Array.ConvertAll(templates,
                t => new SectorEntry { prefab = t.GetComponent<Sector>(), config = bareConfig }),
            presentation = true
        };
        host.playerRig = AssetDatabase.LoadAssetAtPath<PlayerRig>(RigPrefabPath);
        host.observerCamPrefab = AssetDatabase.LoadAssetAtPath<ObserverCam>(ObserverCamPrefabPath);
        host.runRecordPath = recordPath;
        host.buildIdentity = (out BuildIdentity identity, out string failure) =>
        {
            identity = new BuildIdentity { commit = "baseline" };
            failure = null;
            return true;
        };
        hostGo.SetActive(true);
    }

    private static GameObject NewSectorTemplate(string name)
    {
        var go = new GameObject(name);
        go.SetActive(false);
        var sector = go.AddComponent<Sector>();
        sector.SetManifest(null, null, new SectorModule[] { go.AddComponent<ExtractOnCueModule>() });
        return go;
    }

    // Nothing in view: the card and banner are then the only pixels the two sides can differ on.
    // The player stays a framed secondary so the camera has bounds, but no longer the subject it keeps in view.
    private IEnumerator LookAtEmptySpace()
    {
        observer.SetSubject(null, removeOldSubject: false);
        observer.SetManualCenter(EmptySpace);
        observer.SetManualZoom(12f);
        yield return WaitFor(() =>
            (GamePlane.WorldPointToPlane(observer.transform.position) - EmptySpace).magnitude < 0.01f
            && Mathf.Abs(observer.Cam.orthographicSize - 12f) < 0.001f, "camera settled on empty space");
    }

    // One episode per size; the recorder starts on the second step, so a few steps give a few frames.
    private IEnumerator FilmStates(string state)
    {
        foreach (var (width, height) in Sizes)
        {
            Capture.Begin(new CaptureConfig
            {
                clipName = $"{state}-{width}x{height}", runStamp = stamp, width = width, height = height,
                everyFixedSteps = 5,
                configureView = (camera, light) => { camera.enabled = false; light.enabled = false; }
            }, GizmoCaptureProfile.None, new[] { rig.Player }, Session.Projectiles);
            for (var i = 0; i < 3; i++) yield return null;
            for (var i = 0; i < 6; i++)
            {
                FilmStep();
                yield return null;
            }
            Capture.End();
        }
    }

    // Pressed until the prompt goes, which is the banner consuming a fresh press.
    private IEnumerator PressKey(InputTestFixture input, Keyboard keyboard)
    {
        for (var attempt = 0; attempt < 30 && Prompt(); attempt++)
        {
            input.Press(keyboard.enterKey);
            yield return null;
            input.Release(keyboard.enterKey);
            yield return null;
            Debug.Log($"[Baseline] press {attempt}: prompt={Prompt()} banner={Banner()?.alpha}");
        }
    }

    private RunRecapScreen Screen() => hostGo.GetComponentInChildren<RunRecapScreen>();
    private CanvasGroup Banner() => Screen()?.transform.Find("OutcomeIntro")?.GetComponent<CanvasGroup>();
    private bool Prompt() => Screen()?.transform.Find("OutcomeIntro/ContinueHint")?.gameObject.activeSelf == true;
    private CanvasGroup Details() => Screen().transform.Find("Panels/Recap").GetComponent<CanvasGroup>();
    private void Continue() => Screen().transform.Find("Panels/Recap/Continue").GetComponent<Button>().onClick.Invoke();

    private bool InSector(string templateName) =>
        host.ActiveSector && host.ActiveSector.name.StartsWith(templateName, StringComparison.Ordinal);

    private static IEnumerator WaitFor(Func<bool> condition, string what) =>
        AsyncAssert.WaitUntil(condition, 45f, what);
}
#endif

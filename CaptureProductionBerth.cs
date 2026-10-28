using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Capture.GameView;
using Capture.Recording;
using Game;
using Game.Preview;
using Substrate.Sectors.Elements;
using Substrate.Services.Objectives;
using UnityEditor;
using UnityEngine;

public static class CaptureProductionBerth
{
    public static string Main()
    {
        var host = UnityEngine.Object.FindFirstObjectByType<PreviewHost>();
        host.StartCoroutine(Capture(host));
        return "Capturing production extraction presentation";
    }

    private static IEnumerator Capture(PreviewHost host)
    {
        const string output = "D:/amind/git/agent-6/scratch/extraction-marker";
        EditorApplication.isPaused = false;
        Time.timeScale = 1;
        host.RestartPlaytest();
        while (!host.IsReady) yield return null;
        var rig = host.Playtest.GetComponentInChildren<PlayerRig>();
        while (!rig.Player.gameObject.activeInHierarchy) yield return null;
        rig.Player.Damage.SetInvulnerability(1000);
        yield return new WaitForSeconds(6f);
        var observer = host.Playtest.GetComponentInChildren<Cameras.ObserverCam>();
        observer.enabled = false;
        observer.transform.position = new Vector3(-4, -1, -50);
        observer.Cam.orthographicSize = 18;
        Time.timeScale = 0;
        var service = host.Playtest.GetComponent<ObjectiveService>();
        var director = host.Playtest.ActiveSector.GetComponentInChildren<WaveDirector>();
        var capture = ScriptableObject.CreateInstance<GameViewEpisodeCapture>();
        try
        {
            using (capture.PrepareHud1280x720())
            {
                using (var opening = capture.Prepare(new CaptureRequest(output, "production-berth-opening",
                           File.ReadAllText(output + "/capture-commit.txt").Trim(), 1280, 720)))
                {
                    yield return opening.Ready;
                    opening.Start();
                    typeof(WaveDirector).GetField("startTime", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(director, Time.time - 1000f);
                    while (!service.SpineTarget) yield return null;
                    service.SpineTarget.position = new Vector3(138.21f, 58.26f, 0);
                    yield return new WaitForSecondsRealtime(.4f);
                    Save(opening.Finish(), output, "production-berth-opening");
                }
                yield return new WaitForSecondsRealtime(2.1f);
                yield return Record(capture, output, "production-berth-travel");
                service.SpineTarget.position = new Vector3(8, 6, 0);
                for (var frame = 0; frame < 5; frame++) yield return null;
                yield return Record(capture, output, "production-berth-arrival");
                observer.Cam.orthographicSize = 60;
                for (var frame = 0; frame < 5; frame++) yield return null;
                yield return Record(capture, output, "production-berth-arrival-wide");
                File.WriteAllText(output + "/production-capture-complete.txt", "Completed four production HUD captures.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(capture);
            EditorApplication.isPaused = true;
            Time.timeScale = 1;
        }
    }

    private static IEnumerator Record(GameViewEpisodeCapture capture, string output, string label)
    {
        using (var recording = capture.Prepare(new CaptureRequest(output, label,
                   File.ReadAllText(output + "/capture-commit.txt").Trim(), 1280, 720)))
        {
            yield return recording.Ready;
            recording.Start();
            for (var frame = 0; frame < 3; frame++) yield return null;
            Save(recording.Finish(), output, label);
        }
    }

    private static void Save(CaptureResult result, string output, string label)
    {
        var frames = Directory.GetFiles(result.FrameDirectory, "*.png");
        Array.Sort(frames);
        File.Copy(frames[frames.Length - 1], output + "/" + label + ".png", true);
        File.WriteAllText(output + "/" + label + "-capture.json",
            Newtonsoft.Json.JsonConvert.SerializeObject(result));
    }
}

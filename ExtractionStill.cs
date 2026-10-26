using System.Collections;
using System.IO;
using Capture.GameView;
using Capture.Recording;
using Game.Preview;
using UnityEditor;
using UnityEngine;

public static class ExtractionStill
{
    public static string Main()
    {
        var host = Object.FindFirstObjectByType<PreviewHost>();
        host.StartCoroutine(Record());
        return "Capture started";
    }

    private static IEnumerator Record()
    {
        const string root = "D:/amind/git/agent-3/scratch/capture";
        var label = File.ReadAllText(root + "/capture-label.txt").Trim();
        var clock = Time.timeScale;
        Time.timeScale = 0;
        EditorApplication.isPaused = false;
        var capture = ScriptableObject.CreateInstance<GameViewEpisodeCapture>();
        try
        {
            using (capture.PrepareHud1280x720())
            using (var recording = capture.Prepare(new CaptureRequest(root, label,
                       File.ReadAllText(root + "/capture-commit.txt").Trim(), 1280, 720)))
            {
                yield return recording.Ready;
                for (var i = 0; i < 5; i++) yield return null;
                recording.Start();
                for (var i = 0; i < 5; i++) yield return null;
                var result = recording.Finish();
                var frames = Directory.GetFiles(result.FrameDirectory, "*.png");
                System.Array.Sort(frames);
                File.Copy(frames[frames.Length - 1], root + "/" + label + ".png", true);
                File.WriteAllText(root + "/" + label + "-capture.json", Newtonsoft.Json.JsonConvert.SerializeObject(result));
            }
        }
        finally
        {
            Object.DestroyImmediate(capture);
            EditorApplication.isPaused = true;
            Time.timeScale = clock;
        }
    }
}

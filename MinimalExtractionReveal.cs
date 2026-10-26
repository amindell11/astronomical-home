using System.Collections;
using System.IO;
using Capture.GameView;
using Capture.Recording;
using Game.Preview;
using UnityEditor;
using UnityEngine;

public static class MinimalExtractionReveal
{
    public static string Main()
    {
        Object.FindFirstObjectByType<PreviewHost>().StartCoroutine(Record());
        return "Recording opening reveal over the same paused combat state";
    }

    private static IEnumerator Record()
    {
        const string root = "D:/amind/git/agent-3/scratch/capture";
        var comparison = GameObject.Find("Extraction comparison");
        var view = System.Array.Find(comparison.GetComponents<MonoBehaviour>(), component => component.GetType().Name == "MinimalExtractionView");
        var age = view.GetType().GetField("RevealAge");
        var clock = Time.timeScale;
        Time.timeScale = 0;
        EditorApplication.isPaused = false;
        var capture = ScriptableObject.CreateInstance<GameViewEpisodeCapture>();
        try
        {
            using (capture.PrepareHud1280x720())
            using (var recording = capture.Prepare(new CaptureRequest(root, "extraction-minimal-reveal",
                       File.ReadAllText(root + "/capture-commit.txt").Trim(), 1280, 720,
                       measurements: new System.Collections.Generic.Dictionary<string,System.Func<double>> {
                           ["reveal_age"] = () => System.Convert.ToDouble(age.GetValue(view)) })))
            {
                yield return recording.Ready;
                recording.Start();
                var start = Time.unscaledTime;
                while (Time.unscaledTime - start < 3.6f)
                {
                    var elapsed = Time.unscaledTime-start;
                    age.SetValue(view, elapsed);
                    yield return null;
                }
                var result = recording.Finish();
                var frames = Directory.GetFiles(result.FrameDirectory,"*.png"); System.Array.Sort(frames);
                File.Copy(frames[frames.Length/4],root+"/extraction-minimal-opening.png",true);
                File.Copy(frames[frames.Length-1],root+"/extraction-minimal-settled.png",true);
                File.WriteAllText(root+"/extraction-minimal-reveal-capture.json",Newtonsoft.Json.JsonConvert.SerializeObject(result));
            }
        }
        finally
        {
            age.SetValue(view,-1f);
            Object.DestroyImmediate(capture); EditorApplication.isPaused=true; Time.timeScale=clock;
        }
    }
}

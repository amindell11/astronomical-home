using System.Collections;
using System.IO;
using Capture.GameView;
using Capture.Recording;
using Game.Preview;
using UnityEditor;
using UnityEngine;

public static class ExtractionReveal
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
        var card = comparison.transform.Find("Opening reveal").gameObject;
        var mini = comparison.transform.Find("Minimap exit");
        var opacity = card.AddComponent<CanvasGroup>();
        card.SetActive(true);
        opacity.alpha = 0;
        var clock = Time.timeScale;
        Time.timeScale = 0;
        EditorApplication.isPaused = false;
        var capture = ScriptableObject.CreateInstance<GameViewEpisodeCapture>();
        try
        {
            using (capture.PrepareHud1280x720())
            using (var recording = capture.Prepare(new CaptureRequest(root, "extraction-reveal",
                       File.ReadAllText(root + "/capture-commit.txt").Trim(), 1280, 720)))
            {
                yield return recording.Ready;
                recording.Start();
                var start = Time.unscaledTime;
                while (Time.unscaledTime - start < 2.2f)
                {
                    var elapsed = Time.unscaledTime-start;
                    opacity.alpha = Mathf.Min(Mathf.Clamp01(elapsed/.15f), Mathf.Clamp01((2.2f-elapsed)/.5f));
                    mini.localScale = Vector3.one * (1f + .3f * Mathf.Max(0,Mathf.Sin(elapsed*7)) * Mathf.Clamp01(1-elapsed));
                    yield return null;
                }
                var result = recording.Finish();
                var frames = Directory.GetFiles(result.FrameDirectory,"*.png"); System.Array.Sort(frames);
                File.Copy(frames[frames.Length/2],root+"/extraction-reveal.png",true);
                File.WriteAllText(root+"/extraction-reveal-capture.json",Newtonsoft.Json.JsonConvert.SerializeObject(result));
            }
        }
        finally
        {
            card.SetActive(false); mini.localScale=Vector3.one;
            Object.DestroyImmediate(capture); EditorApplication.isPaused=true; Time.timeScale=clock;
        }
    }
}

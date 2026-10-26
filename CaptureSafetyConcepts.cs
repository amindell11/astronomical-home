using System;
using System.Collections;
using System.IO;
using Capture.GameView;
using Capture.Recording;
using Game.Preview;
using Substrate.Services.Objectives;
using UnityEditor;
using UnityEngine;

public static class CaptureSafetyConcepts
{
    public static string Main()
    {
        UnityEngine.Object.FindFirstObjectByType<PreviewHost>().StartCoroutine(Capture());
        return "Capturing both concepts in opening, travel and arrival states.";
    }

    private static IEnumerator Capture()
    {
        const string output="D:/amind/git/agent-6/scratch/extraction-marker";
        var host=UnityEngine.Object.FindFirstObjectByType<PreviewHost>();
        var service=host.Playtest.GetComponent<ObjectiveService>();
        var observer=host.Playtest.GetComponentInChildren<Cameras.ObserverCam>();
        var view=Array.Find(GameObject.Find("Extraction comparison").GetComponents<MonoBehaviour>(),c=>c.GetType().Name=="ExtractionSafetyConcepts");
        var concept=view.GetType().GetField("Concept");
        var reveal=view.GetType().GetField("RevealAge");
        var clock=Time.timeScale;
        Time.timeScale=0; EditorApplication.isPaused=false;
        var capture=ScriptableObject.CreateInstance<GameViewEpisodeCapture>();
        try
        {
            using(capture.PrepareHud1280x720())
            for(var style=0;style<2;style++)
            for(var state=0;state<3;state++)
            {
                concept.SetValue(view,style);
                reveal.SetValue(view,state==0?.75f:-1f);
                service.SpineTarget.position=state==2?new Vector3(8,6,0):new Vector3(138.21f,58.26f,0);
                observer.Cam.orthographicSize=state==2?60:18;
                var label=(style==0?"open-berth":"beacon")+"-"+new[]{"opening","travel","arrival"}[state];
                for(var settle=0;settle<5;settle++) yield return null;
                using(var recording=capture.Prepare(new CaptureRequest(output,label,File.ReadAllText(output+"/capture-commit.txt").Trim(),1280,720)))
                {
                    yield return recording.Ready;
                    recording.Start();
                    for(var frame=0;frame<3;frame++) yield return null;
                    var result=recording.Finish();
                    var frames=Directory.GetFiles(result.FrameDirectory,"*.png"); Array.Sort(frames);
                    File.Copy(frames[frames.Length-1],output+"/"+label+".png",true);
                    File.WriteAllText(output+"/"+label+"-capture.json",Newtonsoft.Json.JsonConvert.SerializeObject(result));
                }
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(capture);
            EditorApplication.isPaused=true; Time.timeScale=clock;
        }
    }
}

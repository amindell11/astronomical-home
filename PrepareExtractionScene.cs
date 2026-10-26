using System.Collections;
using System.Reflection;
using Game;
using Game.Preview;
using Substrate.Sectors.Elements;
using Substrate.Services.Objectives;
using UnityEditor;
using UnityEngine;

public static class PrepareExtractionScene
{
    public static string Main()
    {
        var host = Object.FindFirstObjectByType<PreviewHost>();
        host.StartCoroutine(Prepare(host));
        return "Preparing a protected-player combat comparison";
    }

    private static IEnumerator Prepare(PreviewHost host)
    {
        EditorApplication.isPaused = false;
        Time.timeScale = 1;
        host.RestartPlaytest();
        while (!host.IsReady) yield return null;
        var rig = host.Playtest.GetComponentInChildren<PlayerRig>();
        while (!rig.Player.gameObject.activeInHierarchy) yield return null;
        rig.Player.Damage.SetInvulnerability(1000);
        var director = host.Playtest.ActiveSector.GetComponentInChildren<WaveDirector>();
        var start = typeof(WaveDirector).GetField("startTime", BindingFlags.Instance | BindingFlags.NonPublic);
        yield return null;
        start.SetValue(director, Time.time - 100f);
        yield return new WaitForSeconds(10f);
        start.SetValue(director, Time.time - 140f);
        var objectives = host.Playtest.GetComponent<ObjectiveService>();
        while (objectives.SpineStep != "extract") yield return null;
        yield return null;
        EditorApplication.isPaused = true;
    }
}

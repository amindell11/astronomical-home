using System.Collections;
using System.Collections.Generic;
using System.IO;
using Capture.GameView;
using Capture.Recording;
using Combat.Weapons.Conditions;
using Game;
using Game.Preview;
using UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class MinimalCrosshairStudy
{
    private const string Root = "D:/amind/git/agent-7/scratch/crosshair";
    private static readonly Color Ink = new(.95f, .96f, .92f);
    private static readonly Color Track = new(.04f, .07f, .12f, .86f);

    public static string Main()
    {
        Object.FindFirstObjectByType<PreviewHost>().StartCoroutine(Record());
        return "Capturing minimal firing-state comparison";
    }

    private static IEnumerator Record()
    {
        var host = Object.FindFirstObjectByType<PreviewHost>();
        var rig = host.Playtest.GetComponentInChildren<PlayerRig>();
        var source = rig.Overlay.GetComponentInChildren<WeaponReadoutBuilder>();
        source.GetComponent<CanvasGroup>().alpha = 1;
        foreach (var ammo in source.GetComponentsInChildren<AmmoCounterUI>())
            ammo.GetComponent<CanvasGroup>().alpha = 1;
        var heat = (Heat)source.FirstReadout<IHeatReadout>();
        heat.Reset();
        for (var i = 0; i < 6; i++) heat.ProcessFire();
        var rounds = (Rounds)source.FirstReadout<IAmmoReadout>();
        rounds.Reset();
        rounds.ProcessFire();
        Time.timeScale = 0;
        EditorApplication.isPaused = false;
        var capture = ScriptableObject.CreateInstance<GameViewEpisodeCapture>();
        var labels = new[] { "minimal-idle", "minimal-primary", "minimal-secondary", "minimal-both" };
        var states = new List<object>();
        try
        {
            using (capture.PrepareHud1280x720())
            {
                for (var state = 0; state < labels.Length; state++)
                {
                    Draw(state, heat.HeatPct, rounds.AmmoCount, rounds.MaxAmmo);
                    states.Add(new { capture = labels[state], stagedTrigger = state, heat = heat.HeatPct,
                        rounds = rounds.AmmoCount, capacity = rounds.MaxAmmo, aim = new[] { 880, 430 } });
                    using (var recording = capture.Prepare(new CaptureRequest(Root, labels[state],
                        File.ReadAllText(Root + "/capture-commit.txt").Trim(), 1280, 720)))
                    {
                        yield return recording.Ready;
                        for (var i = 0; i < 5; i++) yield return null;
                        recording.Start();
                        for (var i = 0; i < 5; i++) yield return null;
                        var result = recording.Finish();
                        var frames = Directory.GetFiles(result.FrameDirectory, "*.png");
                        System.Array.Sort(frames);
                        File.Copy(frames[frames.Length - 1], Root + "/" + labels[state] + ".png", true);
                        File.WriteAllText(Root + "/" + labels[state] + "-capture.json", Newtonsoft.Json.JsonConvert.SerializeObject(result));
                    }
                }
            }
            File.WriteAllText(Root + "/minimal-state.json", Newtonsoft.Json.JsonConvert.SerializeObject(states));
        }
        finally
        {
            Object.DestroyImmediate(capture);
            EditorApplication.isPaused = true;
        }
    }

    private static void Draw(int state, float heat, int rounds, int capacity)
    {
        var old = GameObject.Find("Crosshair comparison");
        if (old) Object.DestroyImmediate(old);
        var root = new GameObject("Crosshair comparison", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = .5f;
        var aim = Rect("Aim", root.transform, new Vector2(880, 430), Vector2.zero);
        for (var sign = -1; sign <= 1; sign += 2)
        {
            Mark(aim, new Vector2(sign * 7.5f, 0), new Vector2(5, 1));
            Mark(aim, new Vector2(0, sign * 7.5f), new Vector2(1, 5));
        }
        Mark(aim, Vector2.zero, new Vector2(1, 1));
        if ((state & 1) != 0)
        {
            Box("Heat backing", aim, new Vector2(-18, 0), new Vector2(5, 26), Track);
            Box("Heat", aim, new Vector2(-18, -12 + 12 * heat), new Vector2(3, 24 * heat), Ink);
        }
        if ((state & 2) != 0)
        {
            for (var i = 0; i < capacity; i++)
            {
                var position = new Vector2(18, -6 + i * 12);
                Box("Round backing", aim, position, new Vector2(5, 12), Track);
                Box("Round", aim, position, new Vector2(3, 10), i < rounds ? Ink : new Color(.6f, .64f, .68f, .3f));
            }
        }
        Canvas.ForceUpdateCanvases();
    }

    private static void Mark(Transform parent, Vector2 position, Vector2 size)
    {
        Box("Outline", parent, position, size + Vector2.one * 2, new Color(.015f, .025f, .045f, .85f));
        Box("Crosshair", parent, position, size, Ink);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static void Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }
}

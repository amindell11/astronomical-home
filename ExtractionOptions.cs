using System.IO;
using System.Reflection;
using Cameras;
using Game;
using Game.Preview;
using Substrate.Services.Objectives;
using UI;
using UnityEngine;
using UnityEngine.UI;

public static class ExtractionOptions
{
    public static string Main()
    {
        var old = GameObject.Find("Extraction comparison");
        if (old) Object.DestroyImmediate(old);
        var host = Object.FindFirstObjectByType<PreviewHost>();
        var rig = host.Playtest.GetComponentInChildren<PlayerRig>();
        var marker = rig.Overlay.ObjectiveMarker;
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var icon = (Image)typeof(MinimapObjectiveMarker).GetField("icon", flags).GetValue(marker);
        marker.enabled = false;
        icon.enabled = false;
        var root = new GameObject("Extraction comparison", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        root.GetComponent<Canvas>().sortingOrder = 100;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = .5f;
        var preview = root.AddComponent<ExtractionOptionView>();
        preview.Build(rig, host.Playtest.GetComponent<ObjectiveService>(),
            host.Playtest.GetComponentInChildren<ObserverCam>().Cam,
            (Camera)typeof(MinimapObjectiveMarker).GetField("minimapCam", flags).GetValue(marker));
        return "Comparison UI built in the running scene only";
    }
}

public sealed class ExtractionOptionView : MonoBehaviour
{
    private static readonly Color Gold = new(1f, .8f, .30f, 1f);
    private static readonly Color Dark = new(.025f, .042f, .075f, .98f);
    private PlayerRig rig;
    private ObjectiveService objectives;
    private Camera world;
    private Camera map;
    private RectTransform mini;
    private RectTransform miniArrow;
    private RectTransform pointer;
    private RectTransform pointerArrow;
    private RectTransform reveal;
    private Text distance;
    private bool main;

    public void Build(PlayerRig player, ObjectiveService service, Camera camera, Camera minimap)
    {
        rig = player; objectives = service; world = camera; map = minimap;
        main = File.ReadAllText("D:/amind/git/agent-3/scratch/capture/option.txt").Trim() == "B";
        mini = Root("Minimap exit", transform);
        Badge(mini, 26);
        miniArrow = Root("Minimap direction", mini);
        Chevron(miniArrow, new Vector2(0, 19), 6);
        pointer = Root("Main view direction", transform);
        Badge(pointer, 32);
        pointerArrow = Root("Direction", pointer);
        Chevron(pointerArrow, new Vector2(0, 27), 8);
        var plate = Box("Label backing", pointer, new Vector2(0, -32), new Vector2(140, 26), Dark);
        distance = Label(plate, "EXTRACT", 13, Vector2.zero, new Vector2(138, 25));
        pointer.gameObject.SetActive(main);
        reveal = Root("Opening reveal", transform);
        Box("Reveal backing", reveal, Vector2.zero, new Vector2(320, 52), Dark);
        Box("Reveal rule", reveal, new Vector2(-158, 0), new Vector2(3, 52), Gold);
        Label(reveal, "EXTRACTION OPEN", 19, new Vector2(0, 7), new Vector2(310, 28));
        var sub = Label(reveal, "FOLLOW THE GOLD EXIT MARKER", 11, new Vector2(0, -14), new Vector2(310, 20));
        sub.color = new Color(.82f,.85f,.9f,1);
        reveal.gameObject.SetActive(false);
        LateUpdate();
    }

    private void LateUpdate()
    {
        var scale = new Vector2(1280f / Screen.width, 720f / Screen.height);
        var target = objectives.SpineTarget;
        if (!target) return;
        var corners = new Vector3[4];
        rig.Overlay.MinimapRect.GetWorldCorners(corners);
        var bottom = Vector2.Scale(RectTransformUtility.WorldToScreenPoint(rig.Overlay.WorldCamera, corners[0]), scale);
        var top = Vector2.Scale(RectTransformUtility.WorldToScreenPoint(rig.Overlay.WorldCamera, corners[2]), scale);
        var center = (bottom + top) * .5f;
        var radius = (top.y - bottom.y) * .5f;
        var vp = map.WorldToViewportPoint(target.position);
        var delta = new Vector2(vp.x - .5f, vp.y - .5f) * radius * 2;
        mini.anchoredPosition = center + Vector2.ClampMagnitude(delta, radius - 22);
        miniArrow.localEulerAngles = new Vector3(0,0,Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90);
        miniArrow.gameObject.SetActive(delta.magnitude > radius - 22);
        var point = world.WorldToViewportPoint(target.position);
        var projected = new Vector2(point.x * 1280, point.y * 720);
        var direction = projected - new Vector2(640,360);
        var limit = Mathf.Min(540f / Mathf.Max(.001f, Mathf.Abs(direction.x)), 215f / Mathf.Max(.001f, Mathf.Abs(direction.y)));
        var offscreen = limit < 1;
        var location = offscreen ? new Vector2(640,360) + direction * limit : projected;
        if (offscreen && location.x > 1020 && location.y > 450)
            location.y = 450;
        pointer.anchoredPosition = location;
        pointerArrow.localEulerAngles = new Vector3(0,0,Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90);
        pointerArrow.gameObject.SetActive(offscreen);
        distance.text = "EXTRACT  " + Mathf.RoundToInt(Vector3.Distance(rig.Player.transform.position, target.position)) + " m";
        reveal.anchoredPosition = new Vector2(640, 564);
    }

    private static RectTransform Root(string name, Transform parent)
    {
        var root = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)root.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    private static RectTransform Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var rect = Root(name, parent);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color; image.raycastTarget = false;
        return rect;
    }

    private static void Line(Transform parent, Vector2 a, Vector2 b, float width, Color color)
    {
        var d = b-a;
        var rect = Box("Stroke", parent, (a+b)*.5f, new Vector2(d.magnitude,width),color);
        rect.localEulerAngles = new Vector3(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);
    }

    private static void Badge(Transform parent, float size)
    {
        var border = Box("Exit diamond", parent, Vector2.zero, Vector2.one*size*.7071f,Gold);
        border.localEulerAngles = new Vector3(0,0,45);
        var fill = Box("Contrast backing", parent, Vector2.zero, Vector2.one*(size-5)*.7071f,Dark);
        fill.localEulerAngles = new Vector3(0,0,45);
        Line(parent,new Vector2(-4,-5),new Vector2(-4,5),2,Gold);
        Line(parent,new Vector2(-4,5),new Vector2(1,5),2,Gold);
        Line(parent,new Vector2(-4,-5),new Vector2(1,-5),2,Gold);
        Line(parent,new Vector2(-1,0),new Vector2(7,0),2,Color.white);
        Line(parent,new Vector2(4,-3),new Vector2(7,0),2,Color.white);
        Line(parent,new Vector2(4,3),new Vector2(7,0),2,Color.white);
    }

    private static void Chevron(Transform parent, Vector2 tip, float size)
    {
        Line(parent,tip+new Vector2(-size,-size),tip,3,Gold);
        Line(parent,tip+new Vector2(size,-size),tip,3,Gold);
    }

    private static Text Label(Transform parent, string value, int size, Vector2 position, Vector2 dimensions)
    {
        var rect=Root("Text",parent); rect.anchorMin=rect.anchorMax=Vector2.one*.5f;
        rect.anchoredPosition=position; rect.sizeDelta=dimensions;
        var text=rect.gameObject.AddComponent<Text>();
        text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize=size;
        text.alignment=TextAnchor.MiddleCenter; text.color=Gold; text.text=value; text.raycastTarget=false;
        return text;
    }
}

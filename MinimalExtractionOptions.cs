using System.IO;
using System.Reflection;
using Cameras;
using Game;
using Game.Preview;
using Substrate.Services.Objectives;
using UI;
using UnityEngine;
using UnityEngine.UI;

public static class MinimalExtractionOptions
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
        rig.Overlay.GetComponentInChildren<UI.PlayerState.ObjectiveStatusReadout>().GetComponent<Text>().enabled = false;
        var preview = root.AddComponent<MinimalExtractionView>();
        preview.Build(rig, host.Playtest.GetComponent<ObjectiveService>(),
            host.Playtest.GetComponentInChildren<ObserverCam>().Cam,
            (Camera)typeof(MinimapObjectiveMarker).GetField("minimapCam", flags).GetValue(marker));
        return "Comparison UI built in the running scene only";
    }
}

public sealed class MinimalExtractionView : MonoBehaviour
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
    private CanvasGroup revealOpacity;
    public float RevealAge = -1f;

    public void Build(PlayerRig player, ObjectiveService service, Camera camera, Camera minimap)
    {
        rig = player; objectives = service; world = camera; map = minimap;
        mini = Root("Minimap exit", transform);
        Badge(mini, 26);
        miniArrow = Root("Minimap direction", mini);
        Chevron(miniArrow, new Vector2(0, 19), 6);
        pointer = Root("Main view direction", transform);
        Badge(pointer, 32);
        pointerArrow = Root("Direction", pointer);
        Chevron(pointerArrow, new Vector2(0, 27), 8);
        reveal = Root("Opening reveal", transform);
        Box("Reveal backing", reveal, Vector2.zero, new Vector2(230, 36), Dark);
        Label(reveal, "EXTRACTION OPEN", 18, Vector2.zero, new Vector2(226, 32));
        revealOpacity = reveal.gameObject.AddComponent<CanvasGroup>();
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
        var limit = Mathf.Min(540f / Mathf.Max(.001f, Mathf.Abs(direction.x)), (direction.y < 0 ? 270f : 215f) / Mathf.Max(.001f, Mathf.Abs(direction.y)));
        var offscreen = limit < 1;
        var location = offscreen ? new Vector2(640,360) + direction * limit : projected;
        if (offscreen && location.x > 1020 && location.y > 450)
            location.y = 450;
        var retreat = RevealAge < 0 ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((RevealAge - 1.45f) / .6f));
        pointer.anchoredPosition = Vector2.Lerp(new Vector2(503,625), location, retreat);
        pointerArrow.localEulerAngles = new Vector3(0,0,Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90);
        pointerArrow.gameObject.SetActive(offscreen && retreat > .5f);
        reveal.anchoredPosition = new Vector2(640, 625);
        reveal.gameObject.SetActive(RevealAge >= 0 && RevealAge < 1.55f);
        revealOpacity.alpha = Mathf.Min(Mathf.Clamp01(RevealAge/.15f),Mathf.Clamp01((1.55f-RevealAge)/.3f));
        mini.localScale = Vector3.one * (1f + (RevealAge < 0 ? 0f : .25f * Mathf.Max(0,Mathf.Sin(RevealAge*7)) * Mathf.Clamp01(1-RevealAge)));
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

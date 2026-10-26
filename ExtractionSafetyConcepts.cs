using System.Reflection;
using Cameras;
using Game;
using Game.Preview;
using Substrate.Services.Objectives;
using UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class ExtractionSafetyConcepts : MonoBehaviour
{
    private static readonly Color Ivory = new(.91f, .99f, .96f, 1f);
    private static readonly Color Mint = new(.45f, .94f, .76f, 1f);
    private static readonly Color Ink = new(.015f, .045f, .065f, .85f);
    private readonly RectTransform[] pointers = new RectTransform[2];
    private readonly RectTransform[] minimapIcons = new RectTransform[2];
    private readonly RectTransform[] arrivals = new RectTransform[2];
    private PlayerRig rig;
    private ObjectiveService objectives;
    private Camera world;
    private Camera map;
    private Sprite disc;
    private RectTransform reveal;
    private CanvasGroup revealAlpha;
    public int Concept;
    public float RevealAge = -1f;

    public static string Main()
    {
        var old = GameObject.Find("Extraction comparison");
        if (old) DestroyImmediate(old);
        var host = FindFirstObjectByType<PreviewHost>();
        var player = host.Playtest.GetComponentInChildren<PlayerRig>();
        var marker = player.Overlay.ObjectiveMarker;
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        ((Image)typeof(MinimapObjectiveMarker).GetField("icon", flags).GetValue(marker)).enabled = false;
        marker.enabled = false;
        player.Overlay.GetComponentInChildren<UI.PlayerState.ObjectiveStatusReadout>().GetComponent<Text>().enabled = false;
        var root = new GameObject("Extraction comparison", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        root.GetComponent<Canvas>().sortingOrder = 100;
        var scale = root.GetComponent<CanvasScaler>();
        scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scale.referenceResolution = new Vector2(1280,720);
        scale.matchWidthOrHeight = .5f;
        var view = root.AddComponent<ExtractionSafetyConcepts>();
        view.rig = player;
        view.objectives = host.Playtest.GetComponent<ObjectiveService>();
        view.world = host.Playtest.GetComponentInChildren<ObserverCam>().Cam;
        view.map = (Camera)typeof(MinimapObjectiveMarker).GetField("minimapCam", flags).GetValue(marker);
        view.Build();
        return "Open berth and beacon concepts built in the preview only.";
    }

    private void Build()
    {
        disc = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Visuals/Ui/Hud/circle.png");
        for (var i=0;i<2;i++)
        {
            pointers[i] = Root("Direction "+i,transform);
            minimapIcons[i] = Root("Minimap "+i,transform);
            arrivals[i] = Root("Arrival "+i,transform);
            Symbol(pointers[i],i,1.1f);
            Symbol(minimapIcons[i],i,.9f);
            Arrival(arrivals[i],i);
        }
        reveal=Root("Opening message",transform);
        reveal.anchoredPosition = new Vector2(640,625);
        var plate=Box(reveal,Vector2.zero,new Vector2(200,32),new Color(.015f,.04f,.07f,.64f));
        var label=Root("Extraction open",plate);
        label.anchorMin=label.anchorMax=Vector2.one*.5f;
        label.sizeDelta=new Vector2(192,30);
        var text=label.gameObject.AddComponent<Text>();
        text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text="Extraction open"; text.fontSize=16; text.alignment=TextAnchor.MiddleCenter;
        text.color=Ivory; text.raycastTarget=false;
        Line(reveal,new Vector2(-32,-18),new Vector2(32,-18),1.5f,Mint);
        revealAlpha=reveal.gameObject.AddComponent<CanvasGroup>();
        LateUpdate();
    }

    private void LateUpdate()
    {
        var target=objectives.SpineTarget;
        var viewport=world.WorldToViewportPoint(target.position);
        var projected=new Vector2(viewport.x*1280,viewport.y*720);
        var direction=projected-new Vector2(640,360);
        var limit=Mathf.Min(575f/Mathf.Max(.001f,Mathf.Abs(direction.x)),(direction.y<0?275f:240f)/Mathf.Max(.001f,Mathf.Abs(direction.y)));
        var offscreen=limit<1;
        var location=offscreen?new Vector2(640,360)+direction*limit:projected;
        if(offscreen && location.x>1020 && location.y>445) location.y=430;
        var heading=Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg-90;
        var corners=new Vector3[4]; rig.Overlay.MinimapRect.GetWorldCorners(corners);
        var factor=new Vector2(1280f/Screen.width,720f/Screen.height);
        var lower=Vector2.Scale(RectTransformUtility.WorldToScreenPoint(rig.Overlay.WorldCamera,corners[0]),factor);
        var upper=Vector2.Scale(RectTransformUtility.WorldToScreenPoint(rig.Overlay.WorldCamera,corners[2]),factor);
        var center=(lower+upper)*.5f; var radius=(upper.y-lower.y)*.5f;
        var mapPoint=map.WorldToViewportPoint(target.position);
        var delta=new Vector2(mapPoint.x-.5f,mapPoint.y-.5f)*radius*2;
        for(var i=0;i<2;i++)
        {
            pointers[i].gameObject.SetActive(i==Concept&&offscreen);
            pointers[i].anchoredPosition=location;
            pointers[i].localEulerAngles=new Vector3(0,0,heading);
            minimapIcons[i].gameObject.SetActive(i==Concept);
            minimapIcons[i].anchoredPosition=center+Vector2.ClampMagnitude(delta,radius-24);
            minimapIcons[i].localEulerAngles=new Vector3(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg-90);
            arrivals[i].gameObject.SetActive(i==Concept&&!offscreen);
            arrivals[i].anchoredPosition=projected;
            arrivals[i].localEulerAngles=new Vector3(0,0,heading);
        }
        var age=RevealAge;
        reveal.gameObject.SetActive(age>=0&&age<2.05f);
        revealAlpha.alpha=Mathf.Min(Mathf.Clamp01(age/.22f),Mathf.Clamp01((2.05f-age)/.45f));
    }

    private void Symbol(Transform parent,int style,float scale)
    {
        var shape=Root("Instrument",parent); shape.localScale=Vector3.one*scale;
        Circle(shape,Vector2.zero,27,new Color(.015f,.055f,.065f,.28f));
        if(style==0)
        {
            Arc(shape,14,0,180,2.4f,Ivory);
            Stroke(shape,new Vector2(-14,0),new Vector2(-14,-10),2.4f,Ivory);
            Stroke(shape,new Vector2(14,0),new Vector2(14,-10),2.4f,Ivory);
            Circle(shape,new Vector2(-14,-11),2.5f,Mint);
            Circle(shape,new Vector2(14,-11),2.5f,Mint);
            Circle(shape,new Vector2(0,-15),2.1f,Mint);
            Circle(shape,new Vector2(0,-24),1.5f,new Color(Mint.r,Mint.g,Mint.b,.5f));
        }
        else
        {
            Circle(shape,new Vector2(0,8),11,new Color(Mint.r,Mint.g,Mint.b,.06f));
            Circle(shape,new Vector2(0,8),7,new Color(Mint.r,Mint.g,Mint.b,.13f));
            Circle(shape,new Vector2(0,8),3.4f,Ivory);
            Arc(shape,13,205,335,2.2f,Mint);
            Arc(shape,23,212,328,1.5f,new Color(Ivory.r,Ivory.g,Ivory.b,.68f));
        }
    }

    private void Arrival(Transform parent,int style)
    {
        Circle(parent,Vector2.zero,66,new Color(Mint.r,Mint.g,Mint.b,.035f));
        if(style==0)
        {
            Arc(parent,52,-5,185,2.3f,new Color(Ivory.r,Ivory.g,Ivory.b,.85f));
            Stroke(parent,new Vector2(-51,-4),new Vector2(-47,-27),2.3f,Ivory);
            Stroke(parent,new Vector2(51,-4),new Vector2(47,-27),2.3f,Ivory);
            Circle(parent,new Vector2(-47,-28),3,Mint);
            Circle(parent,new Vector2(47,-28),3,Mint);
            for(var j=0;j<3;j++) Circle(parent,new Vector2(0,-43-j*12),2.5f-j*.4f,new Color(Mint.r,Mint.g,Mint.b,1-j*.24f));
        }
        else
        {
            Arc(parent,52,12,168,1.6f,new Color(Ivory.r,Ivory.g,Ivory.b,.75f));
            Arc(parent,52,205,335,2.2f,Mint);
            Arc(parent,65,220,320,1.4f,new Color(Mint.r,Mint.g,Mint.b,.44f));
            Circle(parent,new Vector2(0,62),3,Ivory);
        }
    }

    private void Circle(Transform parent,Vector2 position,float radius,Color color)
    {
        var rect=Box(parent,position,Vector2.one*radius*2,color);
        rect.GetComponent<Image>().sprite=disc;
    }

    private static void Arc(Transform parent,float radius,float from,float to,float width,Color color)
    {
        for(var pass=0;pass<2;pass++)
        for(var n=0;n<32;n++)
        {
            var a=Mathf.Lerp(from,to,n/32f)*Mathf.Deg2Rad;
            var b=Mathf.Lerp(from,to,(n+1)/32f)*Mathf.Deg2Rad;
            Line(parent,new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,pass==0?width+3:width,pass==0?Ink:color);
        }
    }

    private static void Stroke(Transform parent,Vector2 a,Vector2 b,float width,Color color)
    {
        Line(parent,a,b,width+3,Ink); Line(parent,a,b,width,color);
    }

    private static void Line(Transform parent,Vector2 a,Vector2 b,float width,Color color)
    {
        var delta=b-a;
        var rect=Box(parent,(a+b)*.5f,new Vector2(delta.magnitude+.6f,width),color);
        rect.localEulerAngles=new Vector3(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
    }

    private static RectTransform Box(Transform parent,Vector2 position,Vector2 size,Color color)
    {
        var rect=Root("Mark",parent); rect.anchoredPosition=position; rect.sizeDelta=size;
        var image=rect.gameObject.AddComponent<Image>(); image.color=color; image.raycastTarget=false;
        return rect;
    }

    private static RectTransform Root(string name,Transform parent)
    {
        var rect=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;
        rect.SetParent(parent,false); rect.anchorMin=rect.anchorMax=Vector2.zero; rect.sizeDelta=Vector2.zero;
        return rect;
    }
}

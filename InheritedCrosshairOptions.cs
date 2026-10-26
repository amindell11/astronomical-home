using System.IO;
using System.Reflection;
using Combat.Weapons.Conditions;
using Game;
using Game.Preview;
using UI;
using UnityEngine;
using UnityEngine.UI;

public static class CrosshairOptions
{
    private static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Color Backing = new(.025f,.04f,.075f,.86f);

    public static object Main()
    {
        var old = GameObject.Find("Crosshair comparison");
        if (old) Object.DestroyImmediate(old);
        var host = Object.FindFirstObjectByType<PreviewHost>();
        var rig = host.Playtest.GetComponentInChildren<PlayerRig>();
        var source = rig.Overlay.GetComponentInChildren<WeaponReadoutBuilder>();
        var group = source.GetComponent<CanvasGroup>();
        if (!group) group = source.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0;
        var root = new GameObject("Crosshair comparison", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        root.GetComponent<Canvas>().sortingOrder = 110;
        var scaler=root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1280,720); scaler.matchWidthOrHeight=.5f;
        var aim=Rect("Aim sample",root.transform,new Vector2(540,325),Vector2.zero);
        var split=File.ReadAllText("D:/amind/git/agent-3/scratch/capture/crosshair-option.txt").Trim()=="C";
        if (split)
        {
            Stroke(aim,new Vector2(-14,0),new Vector2(-6,0));
            Stroke(aim,new Vector2(6,0),new Vector2(14,0));
            Stroke(aim,new Vector2(0,-14),new Vector2(0,-6));
            Stroke(aim,new Vector2(0,6),new Vector2(0,14));
        }
        else
        {
            for(var x=-1;x<=1;x+=2) for(var y=-1;y<=1;y+=2)
            {
                Stroke(aim,new Vector2(x*6,y*11),new Vector2(x*11,y*11));
                Stroke(aim,new Vector2(x*11,y*11),new Vector2(x*11,y*6));
            }
        }
        Box("Aim centre",aim,Vector2.zero,new Vector2(2,2),Color.white);
        var builder=Rect("Weapon readouts",aim,Vector2.zero,Vector2.zero).gameObject.AddComponent<WeaponReadoutBuilder>();
        foreach(var name in new[]{"panelPrefab","heatWidgetPrefab","ammoWidgetPrefab","chargeWidgetPrefab","lockWidgetPrefab"})
            typeof(WeaponReadoutBuilder).GetField(name,Fields).SetValue(builder,typeof(WeaponReadoutBuilder).GetField(name,Fields).GetValue(source));
        builder.Build(rig.Player.Weapons);
        var panels=builder.GetComponentsInChildren<WeaponReadoutPanel>();
        for(var i=0;i<panels.Length;i++)
        {
            var panel=panels[i];
            var rect=(RectTransform)panel.transform;
            rect.anchorMin=rect.anchorMax=Vector2.zero; rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=split ? new Vector2(i==0 ? -143 : 29,-23) : new Vector2(30,-22-i*67);
            rect.sizeDelta=new Vector2(114,60);
            var layout=panel.GetComponent<VerticalLayoutGroup>(); if(layout) layout.enabled=false;
            var backing=Box("Contrast backing",panel.transform,new Vector2(57,-25),new Vector2(124,61),Backing);
            backing.SetAsFirstSibling();
            var nameLabel=(Text)typeof(WeaponReadoutPanel).GetField("nameLabel",Fields).GetValue(panel);
            nameLabel.text=(i==0 ? "LMB  " : "RMB  ")+nameLabel.text.ToUpperInvariant();
            nameLabel.fontSize=11; nameLabel.color=new Color(.82f,.88f,.95f);
            SetTopLeft(nameLabel.rectTransform,new Vector2(4,-3),new Vector2(110,16));
            foreach(var ammo in panel.GetComponentsInChildren<AmmoCounterUI>())
                SetTopLeft((RectTransform)ammo.transform,new Vector2(4,-22),new Vector2(105,25));
            foreach(var heat in panel.GetComponentsInChildren<HeatGaugeUI>())
            {
                SetTopLeft((RectTransform)heat.transform,new Vector2(4,-26),new Vector2(100,6));
                var fill=(Image)typeof(HeatGaugeUI).GetField("fillImage",Fields).GetValue(heat);
                fill.fillMethod=Image.FillMethod.Horizontal;
                var track=Box("Heat track",panel.transform,new Vector2(54,-29),new Vector2(100,6),new Color(.16f,.22f,.29f));
                track.SetSiblingIndex(1);
            }
            foreach(var charge in panel.GetComponentsInChildren<ChargeGaugeUI>())
                SetTopLeft((RectTransform)charge.transform,new Vector2(4,-34),new Vector2(100,6));
            foreach(var spinner in panel.GetComponentsInChildren<LockReadoutUI>())
                SetTopLeft((RectTransform)spinner.transform,new Vector2(88,-34),new Vector2(18,18));
        }
        var rounds=source.FirstReadout<IAmmoReadout>();
        return new { option=split?"C":"D", aimSample="540,325 at 1280x720; staged, no aiming changes", ammo=rounds.AmmoCount, max=rounds.MaxAmmo, reloading=rounds.IsReloading, reload=rounds.ReloadProgress, panels=panels.Length };
    }

    private static void SetTopLeft(RectTransform rect,Vector2 position,Vector2 size)
    {
        rect.anchorMin=rect.anchorMax=new Vector2(0,1); rect.pivot=new Vector2(0,1);
        rect.anchoredPosition=position; rect.sizeDelta=size;
    }
    private static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
    {
        var rect=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;
        rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=new Vector2(.5f,.5f);
        rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
    }
    private static RectTransform Box(string name,Transform parent,Vector2 position,Vector2 size,Color color)
    {
        var rect=Rect(name,parent,position,size);var image=rect.gameObject.AddComponent<Image>();
        image.color=color;image.raycastTarget=false;return rect;
    }
    private static void Stroke(Transform parent,Vector2 a,Vector2 b)
    {
        var difference=b-a; var angle=Mathf.Atan2(difference.y,difference.x)*Mathf.Rad2Deg;
        var shadow=Box("Outline",parent,(a+b)*.5f,new Vector2(difference.magnitude+2,4),Color.black);
        shadow.localEulerAngles=new Vector3(0,0,angle);
        var stroke=Box("Reticle",parent,(a+b)*.5f,new Vector2(difference.magnitude,2),Color.white);
        stroke.localEulerAngles=new Vector3(0,0,angle);
    }
}

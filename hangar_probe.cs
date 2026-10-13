using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class HangarProbe
{
    public static object Main()
    {
        var screen = UnityEngine.Object.FindFirstObjectByType<UI.Hangar.HangarScreen>();
        if (!screen) throw new Exception("No live HangarScreen; repro premise failed");
        Canvas.ForceUpdateCanvases();
        var image = (RawImage)typeof(UI.Hangar.HangarScreen).GetField("previewImage", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(screen);
        var stage = UnityEngine.Object.FindFirstObjectByType<UI.Hangar.HangarPreviewStage>();
        var canvas = screen.GetComponent<Canvas>();
        var rt = stage.Texture as RenderTexture;
        var corners = new Vector3[4];
        image.rectTransform.GetWorldCorners(corners);
        var buttons = new List<object>();
        var blocked = 0;
        foreach (var button in screen.GetComponentsInChildren<Button>())
        {
            var center = ((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center);
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, center) };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            var hit = hits.Count > 0 ? hits[0].gameObject : null;
            var reached = hit && hit.GetComponentInParent<Button>() == button;
            if (!reached) blocked++;
            buttons.Add(new { path = button.transform.parent.name + "/" + button.name, interactable = button.IsInteractable(), reached, hit = hit ? hit.name : "none", position = pointer.position.ToString() });
        }
        var report = new {
            verdict = blocked > 0 ? "RED: button centers blocked" : "GREEN: button centers reachable",
            blocked,
            screen = Screen.width + "x" + Screen.height,
            canvasScale = canvas.scaleFactor,
            previewSize = image.rectTransform.sizeDelta.ToString(),
            previewScale = image.rectTransform.localScale.ToString(),
            worldMin = corners[0].ToString(),
            worldMax = corners[2].ToString(),
            raycastTarget = image.raycastTarget,
            texture = rt ? rt.width + "x" + rt.height + " @ " + rt.antiAliasing + "xMSAA" : "none",
            textureId = rt ? rt.GetInstanceID() : 0,
            buttons
        };
        return report;
    }
}

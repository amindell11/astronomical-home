using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class HangarPointerClicks
{
    public static object Main()
    {
        var screen = UnityEngine.Object.FindFirstObjectByType<UI.Hangar.HangarScreen>();
        if (!screen) throw new Exception("No live hangar");
        Canvas.ForceUpdateCanvases();
        var results = new List<object>();
        foreach (var row in screen.GetComponentsInChildren<UI.Hangar.OptionCycler>())
        {
            var fields = BindingFlags.NonPublic | BindingFlags.Instance;
            var label = (Text)typeof(UI.Hangar.OptionCycler).GetField("label", fields).GetValue(row);
            foreach (var fieldName in new[] { "previousButton", "nextButton" })
            {
                var button = (Button)typeof(UI.Hangar.OptionCycler).GetField(fieldName, fields).GetValue(row);
                var rect = (RectTransform)button.transform;
                var pointer = new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                    position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
                };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                if (hits.Count == 0 || !hits[0].gameObject.transform.IsChildOf(button.transform))
                    throw new Exception("Pointer blocked for " + row.name + "/" + button.name);
                var before = label.text;
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
                var after = label.text;
                if (before == after) throw new Exception("Pointer click did not change " + row.name);
                results.Add(new { row = row.name, button = button.name, before, after });
            }
        }
        return new { verdict = "GREEN: all ten pointer clicks changed their selected option", count = results.Count, results };
    }
}

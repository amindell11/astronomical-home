using System;
using System.Collections.Generic;
using System.Linq;
using Ships;
using Ships.Damage;
using UI.PlayerState;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class CapacityLayoutPreview
{
    public static object Show(string scaling, string treatment, bool depleted, float zoom)
    {
        var previous = GameObject.Find("Capacity UI preview");
        if (previous) Object.DestroyImmediate(previous);
        var camera = Camera.main;
        camera.orthographicSize = zoom;
        camera.transform.position = new Vector3(0, -60, -50);
        var ships = Object.FindObjectsByType<Ship>(FindObjectsSortMode.None)
            .Where(s => s.name.StartsWith("Capacity ")).OrderBy(s => s.teamNumber).ThenBy(s => s.Damage.Health.MaxValue).ToArray();
        var nativeBars = ships.SelectMany(s => s.GetComponentsInChildren<StatusBarUI>(true)).ToArray();
        var font = nativeBars[0].GetComponentInChildren<Text>(true).font;
        foreach (var ship in ships)
        {
            ship.Damage.Health.SetFraction(depleted ? (ship.teamNumber == 0 ? 24f / 65f : ship.Damage.Health.MaxValue == 400 ? .5f : .3f) : 1f);
            ship.Damage.Shield.SetFraction(depleted ? (ship.teamNumber == 0 ? 12f / 32f : ship.Damage.Shield.MaxValue == 150 ? .5f : 12f / 50f) : 1f);
        }
        foreach (var bar in nativeBars) bar.gameObject.SetActive(scaling == "native");
        if (scaling == "native") return State(ships, scaling, treatment, depleted, zoom);

        var canvasObject = new GameObject("Capacity UI preview", typeof(RectTransform), typeof(Canvas));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scale = scaling == "bounded" ? Mathf.Clamp(17f / zoom, .8f, 1.15f)
            : scaling == "world" ? 17f / zoom : 1f;
        foreach (var ship in ships)
        {
            var point = camera.WorldToScreenPoint(ship.transform.position);
            var top = point.y;
            if (ship.VisualRig && ship.VisualRig.Silhouette.Count > 0)
            {
                foreach (var renderer in ship.VisualRig.Silhouette)
                {
                    var bounds = renderer.bounds;
                    top = Mathf.Max(top, camera.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z)).y);
                }
            }
            else foreach (var renderer in ship.GetComponentsInChildren<MeshRenderer>()) top = Mathf.Max(top, camera.WorldToScreenPoint(renderer.bounds.max).y);
            var panel = Rect(ship.name, canvas.transform, new Vector2(point.x, top + 12), Vector2.zero);
            panel.localScale = Vector3.one * scale;
            Row(panel, font, "S", ship.Damage.Shield, new Color(0f, .8f, 1f), 18, treatment);
            Row(panel, font, "H", ship.Damage.Health, new Color(.22f, 1f, .15f), 0, treatment);
        }
        Canvas.ForceUpdateCanvases();
        return State(ships, scaling, treatment, depleted, zoom);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0f, .5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        return rt;
    }

    private static Image Block(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Label(string content, Transform parent, Font font, Vector2 position, float width, Color color)
    {
        var text = Rect("Label", parent, position, new Vector2(width, 18)).gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = 12;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = color;
        text.text = content;
        text.raycastTarget = false;
        var shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, .95f);
        shadow.effectDistance = new Vector2(1, -1);
    }

    private static void Row(Transform panel, Font font, string label, Resource resource, Color color, float y, string treatment)
    {
        var width = treatment == "absolute" ? resource.MaxValue * .32f : 80f;
        const float left = -40f;
        Label(label, panel, font, new Vector2(left - 16, y), 14, color);
        Block("Track", panel, new Vector2(left - 1, y), new Vector2(width + 2, 10), new Color(.02f, .04f, .08f, .92f));
        Block("Empty", panel, new Vector2(left, y), new Vector2(width, 8), new Color(.1f, .18f, .24f, 1));
        Block("Remaining", panel, new Vector2(left, y), new Vector2(width * resource.Pct, 8), color);
        if (treatment != "numeric")
            for (var unit = 25; unit < resource.MaxValue; unit += 25)
                Block("25-unit graduation", panel, new Vector2(left + width * unit / resource.MaxValue, y),
                    new Vector2(unit % 100 == 0 ? 2 : 1, 8), new Color(.01f, .025f, .045f, .92f));
        Label($"{Mathf.CeilToInt(resource.CurrentValue)}/{Mathf.CeilToInt(resource.MaxValue)}", panel,
            font, new Vector2(left + width + 5, y), 66, Color.white);
    }

    private static object State(Ship[] ships, string scaling, string treatment, bool depleted, float zoom) => new
    {
        scaling, treatment, depleted, zoom, width = Screen.width, height = Screen.height,
        ships = ships.Select(s => new { s.name, position = s.transform.position.ToString(),
            hull = s.Damage.Health.CurrentValue, hullMax = s.Damage.Health.MaxValue,
            shield = s.Damage.Shield.CurrentValue, shieldMax = s.Damage.Shield.MaxValue }).ToArray()
    };
}


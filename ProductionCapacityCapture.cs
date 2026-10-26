using System.Linq;
using Ships;
using UI.PlayerState;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class ProductionCapacityCapture
{
    public static object Set(float zoom, bool depleted)
    {
        Camera.main.orthographicSize = zoom;
        foreach (var ship in Object.FindObjectsByType<Ship>(FindObjectsSortMode.None).Where(s => s.name.StartsWith("Capacity ")))
        {
            ship.Damage.Health.SetFraction(depleted ? (ship.teamNumber == 0 ? 24f / 65f : ship.Damage.Health.MaxValue == 400 ? .5f : .3f) : 1f);
            ship.Damage.Shield.SetFraction(depleted ? (ship.teamNumber == 0 ? 12f / 32f : ship.Damage.Shield.MaxValue == 150 ? .5f : 12f / 50f) : 1f);
        }
        return new { zoom, depleted };
    }
    public static object Inspect()
    {
        var canvas = Object.FindFirstObjectByType<ShipStatusOverlay>().GetComponent<Canvas>();
        return Object.FindObjectsByType<StatusBarUI>(FindObjectsSortMode.None).Select(bar =>
        {
            var corners = new Vector3[4];
            bar.rectTransform.GetWorldCorners(corners);
            var left = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, corners[0]);
            var right = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, corners[2]);
            return new { value=bar.GetComponentInChildren<Text>().text, width=right.x-left.x, height=right.y-left.y, layer=bar.gameObject.layer };
        }).ToArray();
    }
}

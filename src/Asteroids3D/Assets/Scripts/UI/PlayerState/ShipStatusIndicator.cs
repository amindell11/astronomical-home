using Ships;
using Substrate;
using System.Collections.Generic;
using UnityEngine;

namespace UI.PlayerState
{
    public sealed class ShipStatusIndicator : MonoBehaviour
    {
        private Ship ship;
        private Camera worldCamera;
        private Canvas canvas;
        private RectTransform rect;
        private CanvasGroup visibility;
        private IReadOnlyList<Renderer> silhouette;

        public static ShipStatusIndicator Create(Canvas canvas, Camera worldCamera, Ship ship, Font font)
        {
            var root = new GameObject("Ship status", typeof(RectTransform), typeof(CanvasGroup));
            root.SetActive(false);
            root.layer = canvas.gameObject.layer;
            root.transform.SetParent(canvas.transform, false);
            var indicator = root.AddComponent<ShipStatusIndicator>();
            indicator.ship = ship;
            indicator.worldCamera = worldCamera;
            indicator.canvas = canvas;
            StatusBarUI.Create(root.transform, ship.Damage.Shield, font, new Color(0f, .8f, 1f), 18f);
            StatusBarUI.Create(root.transform, ship.Damage.Health, font, new Color(.22f, 1f, .15f), 0f, pulsesWhenLow: true);
            root.SetActive(true);
            return indicator;
        }

        private void Awake()
        {
            rect = GetComponent<RectTransform>();
            visibility = GetComponent<CanvasGroup>();
            visibility.blocksRaycasts = false;
            visibility.interactable = false;
            silhouette = ship.VisualRig && ship.VisualRig.Silhouette.Count > 0
                ? ship.VisualRig.Silhouette
                : ship.GetComponentsInChildren<MeshRenderer>(true);
        }

        private void LateUpdate()
        {
            var visible = ship && ship.gameObject.activeInHierarchy &&
                (!ship.VisualRig || ship.VisualRig.gameObject.activeInHierarchy) &&
                (ship.CompareTag(TagNames.Player) || !ship.Lock.IsConcealed);
            visibility.alpha = visible ? 1f : 0f;
            if (!visible) return;

            var point = worldCamera.WorldToScreenPoint(ship.transform.position);
            if (point.z < 0f)
            {
                visibility.alpha = 0f;
                return;
            }

            var top = point.y;
            foreach (var renderer in silhouette)
            {
                var bounds = renderer.bounds;
                top = Mathf.Max(top, worldCamera.WorldToScreenPoint(
                    new Vector3(bounds.center.x, bounds.max.y, bounds.center.z)).y);
            }
            point.y = top + 12f;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,
                point, canvas.worldCamera, out var position);
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one * Mathf.Clamp(17f / worldCamera.orthographicSize, .8f, 1.15f);
        }
    }
}

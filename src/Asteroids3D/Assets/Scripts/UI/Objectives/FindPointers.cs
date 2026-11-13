using System;
using System.Collections.Generic;
using Ships;
using Substrate;
using Substrate.Services.Objectives;
using UnityEngine;
using UnityEngine.UI;
using UI.Projection;
using Utils;

namespace UI.Objectives
{
    /// <summary>
    /// Pins unused finds and live, unconcealed enemies beyond the minimap's range to the rim.
    /// A find shows its icon and arrowhead in the find's own colour; an enemy shows only an arrowhead
    /// in the minimap's enemy colour.
    /// </summary>
    public sealed class FindPointers : MonoBehaviour
    {
        private const float IconSize = 22f;
        private const float ArrowSize = 9f;

        private readonly List<Pointer> pointers = new();
        private RectTransform minimap;
        private IObjectiveService objectives;
        private Camera minimapCamera;
        private SubscribedSet<Ship> ships;
        private Color enemyColour;
        private int shown;

        public static FindPointers Create(RectTransform minimap, CanopyProjection projection = null,
            Func<bool> isVisible = null)
        {
            var go = new GameObject("Find pointers", typeof(RectTransform));
            go.layer = minimap.gameObject.layer;
            go.transform.SetParent(minimap, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            var pointers = go.AddComponent<FindPointers>();
            pointers.minimap = minimap;
            InstrumentAppear.Attach(go, projection, () => pointers.shown > 0 && (isVisible?.Invoke() ?? true));
            return pointers;
        }

        private void Awake() => enabled = false;

        public void Initialize(IObjectiveService source, Camera map, SubscribedSet<Ship> ships, Color enemyColour)
        {
            if (source == null || !map || ships == null)
                throw new InvalidOperationException($"FindPointers on '{name}' needs its objective source, minimap camera and ships.");
            objectives = source;
            minimapCamera = map;
            this.ships = ships;
            this.enemyColour = enemyColour;
            enabled = true;
        }

        private void LateUpdate()
        {
            var finds = objectives.Finds;
            var radius = Mathf.Min(minimap.rect.width, minimap.rect.height) * .5f;
            shown = 0;
            for (var i = 0; i < finds.Count; i++)
                ShowBeyondRim(finds[i].Target.position, finds[i].Icon, finds[i].Colour, radius);
            foreach (var ship in ships)
            {
                if (ship.CompareTag(TagNames.Player) || !ship.gameObject.activeInHierarchy || ship.Lock.IsConcealed)
                    continue;
                ShowBeyondRim(ship.transform.position, null, enemyColour, radius);
            }
            for (var i = shown; i < pointers.Count; i++)
                pointers[i].Hide();
        }

        private void ShowBeyondRim(Vector3 world, Sprite sprite, Color colour, float radius)
        {
            var point = minimapCamera.WorldToViewportPoint(world);
            var offset = new Vector2(point.x - .5f, point.y - .5f);
            if (offset.sqrMagnitude <= .25f) return;
            if (shown == pointers.Count) pointers.Add(Pointer.Create(transform));
            pointers[shown++].Show(sprite, colour, offset.normalized, radius);
        }

        private sealed class Pointer
        {
            private Image icon;
            private FindPointerArrow arrow;

            public static Pointer Create(Transform parent)
            {
                var icon = new GameObject("Find pointer", typeof(RectTransform)).AddComponent<Image>();
                icon.gameObject.layer = parent.gameObject.layer;
                icon.transform.SetParent(parent, false);
                icon.rectTransform.sizeDelta = Vector2.one * IconSize;
                icon.raycastTarget = false;
                var arrow = FindPointerArrow.Create(parent, ArrowSize);
                return new Pointer { icon = icon, arrow = arrow };
            }

            public void Show(Sprite sprite, Color colour, Vector2 direction, float radius)
            {
                icon.sprite = sprite;
                icon.color = colour;
                arrow.color = colour;
                icon.rectTransform.anchoredPosition = direction * (radius - ArrowSize - 1f - IconSize * .5f);
                arrow.rectTransform.anchoredPosition = direction * (radius - 1f - ArrowSize * .5f);
                arrow.rectTransform.localEulerAngles =
                    new Vector3(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
                icon.enabled = sprite;
                arrow.enabled = true;
            }

            public void Hide() => icon.enabled = arrow.enabled = false;
        }
    }
}

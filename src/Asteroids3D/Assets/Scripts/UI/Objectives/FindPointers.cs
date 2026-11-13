using System;
using System.Collections.Generic;
using Substrate.Services.Objectives;
using UnityEngine;
using UnityEngine.UI;
using UI.Projection;

namespace UI.Objectives
{
    /// <summary>
    /// Pins each unused find that lies beyond the minimap's range to the minimap's rim, aimed at it. The icon
    /// and the arrow tab take the find's own colour. In range the find's own minimap marker shows instead.
    /// </summary>
    public sealed class FindPointers : MonoBehaviour
    {
        private const float IconSize = 22f;

        private readonly List<Pointer> pointers = new();
        private RectTransform minimap;
        private IObjectiveService objectives;
        private Camera minimapCamera;
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

        public void Initialize(IObjectiveService source, Camera map)
        {
            if (source == null || !map)
                throw new InvalidOperationException($"FindPointers on '{name}' needs its objective source and minimap camera.");
            objectives = source;
            minimapCamera = map;
            enabled = true;
        }

        private void LateUpdate()
        {
            var finds = objectives.Finds;
            var radius = Mathf.Min(minimap.rect.width, minimap.rect.height) * .5f;
            shown = 0;
            for (var i = 0; i < finds.Count; i++)
            {
                var point = minimapCamera.WorldToViewportPoint(finds[i].Target.position);
                var offset = new Vector2(point.x - .5f, point.y - .5f);
                if (offset.sqrMagnitude <= .25f) continue;
                if (shown == pointers.Count) pointers.Add(Pointer.Create(transform));
                pointers[shown++].Show(finds[i].Icon, finds[i].Colour, offset.normalized, radius);
            }
            for (var i = shown; i < pointers.Count; i++)
                pointers[i].Hide();
        }

        private sealed class Pointer
        {
            private Image icon;
            private NavTab arrow;

            public static Pointer Create(Transform parent)
            {
                var icon = new GameObject("Find pointer", typeof(RectTransform)).AddComponent<Image>();
                icon.gameObject.layer = parent.gameObject.layer;
                icon.transform.SetParent(parent, false);
                icon.rectTransform.sizeDelta = Vector2.one * IconSize;
                icon.raycastTarget = false;
                var arrow = NavTab.Create(parent, "Find pointer arrow");
                return new Pointer { icon = icon, arrow = arrow };
            }

            public void Show(Sprite sprite, Color colour, Vector2 direction, float radius)
            {
                icon.sprite = sprite;
                icon.color = colour;
                var centre = direction * (radius - 1f - NavTab.Extent(IconSize, NavTab.FindReach));
                icon.rectTransform.anchoredPosition = centre;
                arrow.Aim(centre, direction, IconSize, NavTab.FindReach, colour);
                icon.enabled = true;
            }

            public void Hide() => icon.enabled = arrow.enabled = false;
        }
    }
}

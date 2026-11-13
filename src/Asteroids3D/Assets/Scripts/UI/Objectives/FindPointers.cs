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
    /// Pins unused finds and live, unconcealed enemies to the minimap rim when they sit beyond
    /// the map's range, and to the screen edge when they leave the world camera's view.
    /// A find shows its icon and arrow tab in the find's own colour; an enemy shows only an arrow tab
    /// in the minimap's enemy colour.
    /// </summary>
    public sealed class FindPointers : MonoBehaviour
    {
        private const float IconSize = 22f;

        private readonly List<Pointer> pointers = new();
        private readonly List<Pointer> screenPointers = new();
        private readonly Vector3[] corners = new Vector3[4];
        private RectTransform minimap;
        private RectTransform screenHolder;
        private IObjectiveService objectives;
        private Camera minimapCamera;
        private Camera worldCamera;
        private Canvas hud;
        private SubscribedSet<Ship> ships;
        private Color enemyColour;
        private CanopyProjection projection;
        private Func<bool> isVisible;
        private int shown;
        private int screenShown;

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
            pointers.projection = projection;
            pointers.isVisible = isVisible;
            InstrumentAppear.Attach(go, projection, () => pointers.shown > 0 && (isVisible?.Invoke() ?? true));
            return pointers;
        }

        private void Awake() => enabled = false;

        public void Initialize(IObjectiveService source, Camera map, Camera world, Canvas hud,
            SubscribedSet<Ship> ships, Color enemyColour)
        {
            if (source == null || !map || !world || !hud || ships == null)
                throw new InvalidOperationException(
                    $"FindPointers on '{name}' needs its objective source, cameras, canvas and ships.");
            objectives = source;
            minimapCamera = map;
            worldCamera = world;
            this.hud = hud;
            this.ships = ships;
            this.enemyColour = enemyColour;
            var holder = new GameObject("Screen pointers", typeof(RectTransform));
            holder.layer = hud.gameObject.layer;
            holder.transform.SetParent(hud.transform, false);
            screenHolder = (RectTransform)holder.transform;
            screenHolder.anchorMin = Vector2.zero;
            screenHolder.anchorMax = Vector2.one;
            screenHolder.sizeDelta = Vector2.zero;
            InstrumentAppear.Attach(holder, projection, () => screenShown > 0 && (isVisible?.Invoke() ?? true));
            enabled = true;
        }

        private void LateUpdate()
        {
            var finds = objectives.Finds;
            var radius = Mathf.Min(minimap.rect.width, minimap.rect.height) * .5f;
            var mapBounds = ObjectiveMarkers.MapBounds(minimap, hud, corners);
            shown = 0;
            screenShown = 0;
            for (var i = 0; i < finds.Count; i++)
                PointAt(finds[i].Target.position, finds[i].Icon, finds[i].Colour, radius, mapBounds);
            foreach (var ship in ships)
            {
                if (ship.CompareTag(TagNames.Player) || !ship.gameObject.activeInHierarchy || ship.Lock.IsConcealed)
                    continue;
                PointAt(ship.transform.position, null, enemyColour, radius, mapBounds);
            }
            for (var i = shown; i < pointers.Count; i++)
                pointers[i].Hide();
            for (var i = screenShown; i < screenPointers.Count; i++)
                screenPointers[i].Hide();
        }

        private void PointAt(Vector3 world, Sprite sprite, Color colour, float radius, Rect mapBounds)
        {
            ShowBeyondRim(world, sprite, colour, radius);
            ShowAtScreenEdge(world, sprite, colour, mapBounds);
        }

        private void ShowBeyondRim(Vector3 world, Sprite sprite, Color colour, float radius)
        {
            var point = minimapCamera.WorldToViewportPoint(world);
            var offset = new Vector2(point.x - .5f, point.y - .5f);
            if (offset.sqrMagnitude <= .25f) return;
            if (shown == pointers.Count) pointers.Add(Pointer.Create(transform, screen: false));
            var direction = offset.normalized;
            var centre = direction * (radius - 1f - NavTab.Extent(IconSize, NavTab.FindReach));
            pointers[shown++].Show(sprite, colour, centre, direction);
        }

        private void ShowAtScreenEdge(Vector3 world, Sprite sprite, Color colour, Rect mapBounds)
        {
            var projected = worldCamera.WorldToScreenPoint(world);
            var viewport = worldCamera.pixelRect;
            var direction = (Vector2)projected - viewport.center;
            var point = (Vector2)projected;
            if (projected.z < 0f)
            {
                direction = -direction;
                if (direction.sqrMagnitude <= .0001f)
                    direction = Vector2.down;
                point = viewport.center + direction.normalized * (viewport.width + viewport.height);
            }

            if (!ObjectiveMarkers.PinToScreenEdge(point, viewport, hud, mapBounds, out var centre))
                return;
            if (screenShown == screenPointers.Count)
                screenPointers.Add(Pointer.Create(screenHolder, screen: true));
            screenPointers[screenShown++].Show(sprite, colour, centre, direction.normalized);
        }

        private sealed class Pointer
        {
            private Image icon;
            private NavTab arrow;

            public static Pointer Create(Transform parent, bool screen)
            {
                var name = screen ? "Screen pointer" : "Find pointer";
                var icon = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
                icon.gameObject.layer = parent.gameObject.layer;
                icon.transform.SetParent(parent, false);
                if (screen)
                    icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = Vector2.zero;
                icon.rectTransform.sizeDelta = Vector2.one * IconSize;
                icon.raycastTarget = false;
                var arrow = NavTab.Create(parent, name + " arrow");
                if (screen)
                    arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = Vector2.zero;
                return new Pointer { icon = icon, arrow = arrow };
            }

            public void Show(Sprite sprite, Color colour, Vector2 centre, Vector2 direction)
            {
                icon.sprite = sprite;
                icon.color = colour;
                icon.rectTransform.anchoredPosition = centre;
                arrow.Aim(centre, direction, IconSize, NavTab.FindReach, colour);
                icon.enabled = sprite;
            }

            public void Hide() => icon.enabled = arrow.enabled = false;
        }
    }
}

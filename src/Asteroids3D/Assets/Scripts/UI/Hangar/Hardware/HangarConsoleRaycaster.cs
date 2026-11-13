using System;
using System.Collections.Generic;
using UI.Hangar.Displays;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI.Hangar.Hardware
{
    /// <summary>
    /// Pointers on the composite image. The hardware camera's projection is custom, so the ray is
    /// the pointer's place in that image, and a glass hit is a pixel on that display's canvas.
    /// </summary>
    public sealed class HangarConsoleRaycaster : BaseRaycaster
    {
        private struct Target
        {
            public MeshSurface Surface;
            public Transform Transform;
            public GameObject Control;
            public HangarDisplay Display;
            public int DisplayIndex;
        }

        private HangarConsole console;
        private Canvas compositeCanvas;
        private Target[] targets;

        public override Camera eventCamera => console.HardwareCamera;
        public override int sortOrderPriority => HangarConsole.CompositeSortingOrder;

        protected override void Awake()
        {
            base.Awake();
            console = GetComponent<HangarConsole>();
            if (!console || !console.CompositeImage)
                throw new InvalidOperationException("HangarConsoleRaycaster needs the console and its composite image.");
            compositeCanvas = console.CompositeImage.canvas;
            targets = new[]
            {
                Control(console.SelectorKnob),
                Control(console.PreviousButton),
                Control(console.NextButton),
                Control(console.LaunchButton),
                Glass(console.MainScreenGlass, console.MainDisplay, 0),
                Glass(console.DetailScreenGlass, console.DetailDisplay, 1)
            };
        }

        public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
        {
            if (!TryViewport(eventData.position, out var viewport)) return;
            var hit = Hit(viewport, out var distance, out var world, out var depth, out var sortingLayer,
                out var sortingOrder, out var displayIndex);
            if (!hit) return;
            resultAppendList.Add(new RaycastResult
            {
                gameObject = hit,
                module = this,
                distance = distance,
                index = resultAppendList.Count,
                depth = depth,
                sortingLayer = sortingLayer,
                sortingOrder = sortingOrder,
                screenPosition = eventData.position,
                displayIndex = displayIndex,
                worldPosition = world
            });
        }

        internal GameObject HitAt(Vector2 viewport) =>
            Hit(viewport, out _, out _, out _, out _, out _, out _);

        private GameObject Hit(Vector2 viewport, out float distance, out Vector3 world, out int depth,
            out int sortingLayer, out int sortingOrder, out int displayIndex)
        {
            var ray = console.HardwareCamera.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
            var nearest = float.PositiveInfinity;
            var found = -1;
            Vector2 uv = default;
            for (var i = 0; i < targets.Length; i++)
            {
                if (!targets[i].Surface.Raycast(ray, targets[i].Transform, out var hitDistance, out var hitUv))
                    continue;
                if (hitDistance >= nearest) continue;
                nearest = hitDistance;
                found = i;
                uv = hitUv;
            }

            distance = nearest;
            world = ray.GetPoint(nearest);
            depth = 0;
            sortingLayer = compositeCanvas.sortingLayerID;
            sortingOrder = compositeCanvas.sortingOrder;
            displayIndex = 0;
            if (found < 0) return null;

            var target = targets[found];
            if (target.Control) return target.Control;

            displayIndex = target.DisplayIndex;
            var pixel = new Vector2(uv.x * target.Display.Resolution.x, uv.y * target.Display.Resolution.y);
            var graphic = TopGraphic(target.Display.Canvas, target.Display.DisplayCamera, pixel);
            if (!graphic) return null;
            depth = graphic.depth;
            return graphic.gameObject;
        }

        private bool TryViewport(Vector2 screen, out Vector2 viewport)
        {
            viewport = default;
            var rectTransform = console.CompositeImage.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, null, out var local))
                return false;
            var rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f || !rect.Contains(local)) return false;
            viewport = new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
            return true;
        }

        private static Graphic TopGraphic(Canvas canvas, Camera camera, Vector2 pixel)
        {
            var graphics = GraphicRegistry.GetRaycastableGraphicsForCanvas(canvas);
            Graphic best = null;
            for (var i = 0; i < graphics.Count; i++)
            {
                var graphic = graphics[i];
                if (!graphic || graphic.depth == -1) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(
                        graphic.rectTransform, pixel, camera, graphic.raycastPadding))
                    continue;
                if (!graphic.Raycast(pixel, camera)) continue;
                if (!best || graphic.depth > best.depth) best = graphic;
            }
            return best;
        }

        private static Target Control(Transform part) => new()
        {
            Surface = Read(part),
            Transform = part,
            Control = part.gameObject
        };

        private static Target Glass(MeshRenderer glass, HangarDisplay display, int index) => new()
        {
            Surface = Read(glass.transform),
            Transform = glass.transform,
            Display = display,
            DisplayIndex = index
        };

        private static MeshSurface Read(Transform part)
        {
            var filter = part.GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh)
                throw new InvalidOperationException($"{part.name} needs a mesh.");
            return new MeshSurface(filter.sharedMesh);
        }
    }
}

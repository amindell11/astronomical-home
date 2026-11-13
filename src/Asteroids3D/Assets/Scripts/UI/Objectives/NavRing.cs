using UnityEngine;
using UnityEngine.UI;

namespace UI.Objectives
{
    /// <summary>
    /// A thin ring around a nav icon, sized from the icon's rect, that drains clockwise from the top as its fill falls.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NavRing : MaskableGraphic
    {
        private const float Width = 4.5f / 64f; // The family's glyph stroke, as a share of the icon's size.
        private const int Segments = 48;

        private float radius;
        private float? fill;

        public float? Fill
        {
            get => fill;
            set
            {
                if (value == fill) return;
                fill = value;
                SetVerticesDirty();
            }
        }

        public static NavRing Create(RectTransform icon, float reach)
        {
            var ring = new GameObject(icon.name + " ring", typeof(RectTransform)).AddComponent<NavRing>();
            ring.gameObject.layer = icon.gameObject.layer;
            ring.transform.SetParent(icon, false);
            ring.rectTransform.anchorMin = Vector2.zero;
            ring.rectTransform.anchorMax = Vector2.one;
            ring.rectTransform.sizeDelta = Vector2.zero;
            ring.radius = reach + NavTab.Gap + Width * .5f;
            ring.raycastTarget = false;
            return ring;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (fill is not float left) return;
            var size = rectTransform.rect.width;
            var span = Mathf.Clamp01(left) * 360f;
            var step = 360f / Segments;
            var outer = (radius + Width * .5f) * size;
            var inner = (radius - Width * .5f) * size;
            for (var i = 0; i < Segments && i * step < span; i++)
            {
                var a = (90f - i * step) * Mathf.Deg2Rad;
                var b = (90f - Mathf.Min((i + 1) * step, span)) * Mathf.Deg2Rad;
                var from = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var to = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                var start = mesh.currentVertCount;
                mesh.AddVert(from * inner, color, Vector2.zero);
                mesh.AddVert(from * outer, color, Vector2.zero);
                mesh.AddVert(to * outer, color, Vector2.zero);
                mesh.AddVert(to * inner, color, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2);
                mesh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}

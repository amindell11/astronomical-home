using System;
using UnityEngine;
using UnityEngine.UI;

namespace UI.PlayerState.Alerts
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AlertInk : MaskableGraphic
    {
        private Action<VertexHelper, Rect> draw;

        public static AlertInk Create(Transform parent, string name, Action<VertexHelper, Rect> draw)
        {
            if (draw == null) throw new ArgumentNullException(nameof(draw));
            var ink = new GameObject(name, typeof(RectTransform)).AddComponent<AlertInk>();
            ink.gameObject.layer = parent.gameObject.layer;
            ink.transform.SetParent(parent, false);
            ink.rectTransform.anchorMin = Vector2.zero;
            ink.rectTransform.anchorMax = Vector2.one;
            ink.rectTransform.sizeDelta = Vector2.zero;
            ink.raycastTarget = false;
            ink.draw = draw;
            ink.SetVerticesDirty();
            return ink;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            draw(mesh, rectTransform.rect);
        }

        public static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            Color ab, Color cd)
        {
            var start = mesh.currentVertCount;
            mesh.AddVert(a, ab, Vector2.zero);
            mesh.AddVert(b, ab, Vector2.zero);
            mesh.AddVert(c, cd, Vector2.zero);
            mesh.AddVert(d, cd, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start + 2, start + 3, start);
        }

        public static void Stroke(VertexHelper mesh, Vector2 from, Vector2 to, float width, Color tint)
        {
            var along = (to - from).normalized;
            var side = new Vector2(-along.y, along.x) * (width * .5f);
            // Half-width overhang closes the joint where two strokes meet.
            from -= along * (width * .5f);
            to += along * (width * .5f);
            Quad(mesh, from - side, from + side, to + side, to - side, tint, tint);
        }
    }
}

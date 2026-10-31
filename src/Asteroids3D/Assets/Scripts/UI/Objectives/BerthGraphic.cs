using UnityEngine;
using UnityEngine.UI;

namespace UI.Objectives
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BerthGraphic : MaskableGraphic
    {
        private static readonly Color Ivory = new(.91f, .99f, .96f, 1f);
        public static readonly Color ExtractionAccent = new(.45f, .94f, .76f, 1f);
        private static readonly Color Ink = new(.015f, .045f, .065f, .85f);
        private bool minimap;
        private Color accent = ExtractionAccent;

        public Color Accent
        {
            get => accent;
            set
            {
                if (accent == value) return;
                accent = value;
                SetVerticesDirty();
            }
        }

        public static BerthGraphic Create(Transform parent, bool minimap)
        {
            var mark = new GameObject(minimap ? "Extraction minimap" : "Extraction direction",
                typeof(RectTransform)).AddComponent<BerthGraphic>();
            mark.gameObject.layer = parent.gameObject.layer;
            mark.transform.SetParent(parent, false);
            mark.rectTransform.anchorMin = mark.rectTransform.anchorMax = Vector2.zero;
            mark.rectTransform.sizeDelta = Vector2.one * (minimap ? 13.75f : 37.125f);
            mark.minimap = minimap;
            mark.raycastTarget = false;
            mark.enabled = false;
            return mark;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var scale = rectTransform.rect.width / (minimap ? 22f : 54f);
            if (minimap)
            {
                Disc(mesh, Vector2.zero, 11, new Color(Ink.r, Ink.g, Ink.b, .92f), scale);
                Arch(mesh, 7, 5, 2.8f, scale);
                Disc(mesh, new Vector2(0, -3), 2.1f, accent, scale);
                return;
            }

            Disc(mesh, Vector2.zero, 27, new Color(.015f, .055f, .065f, .28f), scale);
            Arch(mesh, 14, 10, 2.4f, scale);
            Disc(mesh, new Vector2(-14, -11), 2.5f, accent, scale);
            Disc(mesh, new Vector2(14, -11), 2.5f, accent, scale);
            Disc(mesh, new Vector2(0, -15), 2.1f, accent, scale);
            Disc(mesh, new Vector2(0, -24), 1.5f, new Color(accent.r, accent.g, accent.b, .5f), scale);
        }

        private static void Arch(VertexHelper mesh, float radius, float leg, float width, float scale)
        {
            for (var pass = 0; pass < 2; pass++)
            {
                var tint = pass == 0 ? Ink : Ivory;
                var thickness = pass == 0 ? width + 3f : width;
                for (var i = 0; i < 32; i++)
                {
                    var a = i * Mathf.PI / 32;
                    var b = (i + 1) * Mathf.PI / 32;
                    Line(mesh, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                        new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, thickness, tint, scale);
                }
                Line(mesh, new Vector2(-radius, 0), new Vector2(-radius, -leg), thickness, tint, scale);
                Line(mesh, new Vector2(radius, 0), new Vector2(radius, -leg), thickness, tint, scale);
            }
        }

        private static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint, float scale)
        {
            var direction = (b - a).normalized;
            var normal = new Vector2(-direction.y, direction.x) * (width * .5f);
            a -= direction * .3f;
            b += direction * .3f;
            var start = mesh.currentVertCount;
            mesh.AddVert((a - normal) * scale, tint, Vector2.zero);
            mesh.AddVert((a + normal) * scale, tint, Vector2.zero);
            mesh.AddVert((b + normal) * scale, tint, Vector2.zero);
            mesh.AddVert((b - normal) * scale, tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        private static void Disc(VertexHelper mesh, Vector2 center, float radius, Color tint, float scale)
        {
            var start = mesh.currentVertCount;
            mesh.AddVert(center * scale, tint, Vector2.zero);
            for (var i = 0; i <= 32; i++)
            {
                var angle = i * Mathf.PI * 2 / 32;
                mesh.AddVert((center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius) * scale,
                    tint, Vector2.zero);
                if (i > 0) mesh.AddTriangle(start, start + i, start + i + 1);
            }
        }
    }
}

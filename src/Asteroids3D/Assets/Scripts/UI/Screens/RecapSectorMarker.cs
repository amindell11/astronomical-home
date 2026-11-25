using UnityEngine;
using UnityEngine.UI;

namespace UI.Screens
{
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class RecapSectorMarker : MaskableGraphic
    {
        public bool Reached { get; set; }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            var radius = Mathf.Min(rect.width, rect.height) * .5f;
            const int segments = 40;
            for (var i = 0; i < segments; i++)
            {
                if (Reached && i > 25) break;
                var a = (i * 360f / segments + 20f) * Mathf.Deg2Rad;
                var b = ((i + 1) * 360f / segments + 20f) * Mathf.Deg2Rad;
                var start = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var end = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                var inner = Reached ? radius - 1f : 0f;
                var n = mesh.currentVertCount;
                mesh.AddVert(rect.center + start * radius, color, Vector2.zero);
                mesh.AddVert(rect.center + end * radius, color, Vector2.zero);
                mesh.AddVert(rect.center + end * inner, color, Vector2.zero);
                mesh.AddVert(rect.center + start * inner, color, Vector2.zero);
                mesh.AddTriangle(n, n + 1, n + 2);
                mesh.AddTriangle(n, n + 2, n + 3);
            }
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace UI.Screens.Windows
{
    /// <summary>
    /// A rectangle with its corners cut at the house 40°, drawn filled or as an outline of
    /// <see cref="Stroke"/> width. A window's frame, body and title bar are three of these on one
    /// rectangle, so their cut corners line up.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class ChamferedShape : MaskableGraphic
    {
        private const float Slope = .8391f;
        // Keeps the diagonal as thick as the straight edges when the outline is inset.
        private const float InsetChamfer = .636f;
        private const int Corners = 8;

        private readonly Vector2[] outer = new Vector2[Corners];
        private readonly Vector2[] inner = new Vector2[Corners];

        public float Chamfer { get; set; }
        public float Stroke { get; set; }
        public bool SquareBottom { get; set; }

        /// <summary>The chamfer as drawn: never so deep that two cuts on one side cross.</summary>
        internal float DrawnChamfer => Fit(rectTransform.rect, Chamfer);

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            var chamfer = DrawnChamfer;
            Trace(rect, chamfer, outer);
            Color32 tint = color;
            if (Stroke <= 0f)
            {
                mesh.AddVert(rect.center, tint, Vector4.zero);
                foreach (var point in outer) mesh.AddVert(point, tint, Vector4.zero);
                for (var i = 0; i < Corners; i++) mesh.AddTriangle(0, 1 + i, 1 + (i + 1) % Corners);
                return;
            }

            var stroke = Mathf.Min(Stroke, rect.width * .5f, rect.height * .5f);
            var inset = new Rect(rect.xMin + stroke, rect.yMin + stroke, rect.width - 2f * stroke,
                rect.height - 2f * stroke);
            Trace(inset, Fit(inset, Mathf.Max(0f, chamfer - stroke * InsetChamfer)), inner);
            for (var i = 0; i < Corners; i++)
            {
                mesh.AddVert(outer[i], tint, Vector4.zero);
                mesh.AddVert(inner[i], tint, Vector4.zero);
            }
            for (var i = 0; i < Corners; i++)
            {
                var a = 2 * i;
                var b = 2 * ((i + 1) % Corners);
                mesh.AddTriangle(a, b, b + 1);
                mesh.AddTriangle(a, b + 1, a + 1);
            }
        }

        private float Fit(Rect rect, float chamfer) =>
            Mathf.Max(0f, Mathf.Min(chamfer, rect.width * .5f, SquareBottom ? chamfer : rect.height * .5f / Slope));

        private void Trace(Rect rect, float chamfer, Vector2[] points)
        {
            // A square bottom shorter than the cut crops the diagonal at its bottom edge.
            var drop = Mathf.Min(chamfer * Slope, rect.height);
            var side = chamfer - drop / Slope;
            var bottomChamfer = SquareBottom ? side : chamfer;
            var bottomDrop = SquareBottom ? 0f : drop;
            points[0] = new Vector2(rect.xMin + chamfer, rect.yMax);
            points[1] = new Vector2(rect.xMax - chamfer, rect.yMax);
            points[2] = new Vector2(rect.xMax - side, rect.yMax - drop);
            points[3] = new Vector2(rect.xMax - side, rect.yMin + bottomDrop);
            points[4] = new Vector2(rect.xMax - bottomChamfer, rect.yMin);
            points[5] = new Vector2(rect.xMin + bottomChamfer, rect.yMin);
            points[6] = new Vector2(rect.xMin + side, rect.yMin + bottomDrop);
            points[7] = new Vector2(rect.xMin + side, rect.yMax - drop);
        }
    }
}

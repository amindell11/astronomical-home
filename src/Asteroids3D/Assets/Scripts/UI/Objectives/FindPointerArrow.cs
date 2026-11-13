using UnityEngine;
using UnityEngine.UI;

namespace UI.Objectives
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FindPointerArrow : MaskableGraphic
    {
        public static FindPointerArrow Create(Transform parent, float size)
        {
            var arrow = new GameObject("Find pointer arrow", typeof(RectTransform)).AddComponent<FindPointerArrow>();
            arrow.gameObject.layer = parent.gameObject.layer;
            arrow.transform.SetParent(parent, false);
            arrow.rectTransform.sizeDelta = Vector2.one * size;
            arrow.raycastTarget = false;
            return arrow;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var half = rectTransform.rect.size * .5f;
            mesh.AddVert(new Vector2(0f, half.y), color, Vector2.zero);
            mesh.AddVert(new Vector2(half.x, -half.y), color, Vector2.zero);
            mesh.AddVert(new Vector2(-half.x, -half.y), color, Vector2.zero);
            mesh.AddTriangle(0, 1, 2);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace UI.Objectives
{
    /// <summary>
    /// Triangular tab drawn just outside a nav icon, in the icon's colour, aimed at a target past the map or the screen.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NavTab : MaskableGraphic
    {
        private const float Length = .36f;
        private const float WidthRatio = 1.24f;
        private const float Gap = 4f / 64f;

        public const float FindReach = 29.5f / 64f;
        public const float ExitReach = 27f / 64f;
        public const float LullReach = 30f / 64f;

        public static NavTab Create(Transform parent, string name)
        {
            var tab = new GameObject(name, typeof(RectTransform)).AddComponent<NavTab>();
            tab.gameObject.layer = parent.gameObject.layer;
            tab.transform.SetParent(parent, false);
            tab.raycastTarget = false;
            return tab;
        }

        public static float Extent(float iconSize, float reach) => iconSize * (reach + Gap + Length);

        public void Aim(Vector2 iconCentre, Vector2 direction, float iconSize, float reach, Color colour)
        {
            rectTransform.sizeDelta = new Vector2(Length * WidthRatio * iconSize, Length * iconSize);
            rectTransform.anchoredPosition = iconCentre + direction * iconSize * (reach + Gap + Length * .5f);
            rectTransform.localEulerAngles =
                new Vector3(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            color = colour;
            enabled = true;
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

using UnityEngine;
using UnityEngine.UI;

namespace UI.Screens.Windows
{
    /// <summary>
    /// A window's dark body on the under canvas, where it can darken the view that the canopy's ink
    /// can only add light to. Each frame it takes the place, size, unfold clip and inherited fade of its
    /// window's back in the ink tree; it never changes its window.
    /// </summary>
    internal sealed class WindowShade : MonoBehaviour
    {
        private Graphic back;
        private RectTransform target;
        private Transform inkRoot;
        private RectMask2D clip;
        private RectTransform rect;
        private ChamferedShape shape;

        internal static WindowShade Create(ScreenLayers layers, Graphic back, RectMask2D clip, float chamfer)
        {
            var target = back.rectTransform;
            var rect = ScreenKit.Box(layers.Under, target.parent.name + " Shade", 0f, 0f, 0f, 0f);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            var shade = rect.gameObject.AddComponent<WindowShade>();
            shade.back = back;
            shade.target = target;
            shade.inkRoot = layers.transform;
            shade.clip = clip;
            shade.rect = rect;
            shade.shape = WindowKit.Shape(rect, WindowKit.Body, chamfer, 0f);
            shade.Follow();
            return shade;
        }

        private void LateUpdate() => Follow();

        private void Follow()
        {
            var bounds = target.rect;
            var padding = clip ? clip.padding : Vector4.zero;
            var min = bounds.min + new Vector2(padding.x, padding.y);
            var max = bounds.max - new Vector2(padding.z, padding.w);
            // Both canvases fill the screen with one scaler, so ink-canvas coordinates are under-canvas coordinates.
            var low = inkRoot.InverseTransformPoint(target.TransformPoint(min));
            var high = inkRoot.InverseTransformPoint(target.TransformPoint(max));
            rect.localPosition = (low + high) * .5f;
            rect.sizeDelta = new Vector2(Mathf.Max(0f, high.x - low.x), Mathf.Max(0f, high.y - low.y));

            var alpha = WindowKit.Body.a * back.canvasRenderer.GetInheritedAlpha();
            shape.color = new Color(WindowKit.Body.r, WindowKit.Body.g, WindowKit.Body.b, alpha);
            shape.enabled = target.gameObject.activeInHierarchy && alpha > 0f && max.x > min.x && max.y > min.y;
        }
    }
}

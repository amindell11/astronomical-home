using UnityEngine;

namespace UI.Placement
{
    /// <summary>
    /// Pins a rect to a screen pixel of the screen-space canvas whose rect is its parent.
    /// It works in screen pixels over the canvas's bottom-left corner because Unity re-aligns a
    /// Screen Space - Camera canvas to its moving camera only after LateUpdate: converting through
    /// the canvas transform trails one frame of camera travel.
    /// </summary>
    public static class ScreenPin
    {
        public static void Place(RectTransform rect, Canvas canvas, Vector2 screenPoint)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.anchoredPosition = screenPoint / canvas.scaleFactor;
        }
    }
}

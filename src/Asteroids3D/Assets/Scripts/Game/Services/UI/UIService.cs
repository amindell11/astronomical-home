using UI;
using UnityEngine;

namespace Game.Services.UI
{
    public class UIService : IUIService
    {
        public Overlay ActiveOverlay { get; private set; }

        public void Show(Overlay overlay, UnityEngine.Camera uiCamera)
        {
            Clear();
            ActiveOverlay = overlay;
            overlay.SetCanvasWorldCamera(uiCamera);
        }

        public void Clear()
        {
            if (ActiveOverlay)
                Object.Destroy(ActiveOverlay.gameObject);
            ActiveOverlay = null;
        }
    }
}

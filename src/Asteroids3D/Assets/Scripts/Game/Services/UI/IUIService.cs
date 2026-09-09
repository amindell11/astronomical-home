using UI;
using UnityEngine;

namespace Game.Services.UI
{
    public interface IUIService
    {
        Overlay ActiveOverlay { get; }
        void Show(Overlay overlay, UnityEngine.Camera uiCamera);
        void Clear();
    }
}

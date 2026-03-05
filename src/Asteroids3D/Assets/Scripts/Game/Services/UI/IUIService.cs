using UI;
using UnityEngine;

namespace Game.Services
{
    public interface IUIService
    {
        Overlay ActiveOverlay { get; }
        void Show(Overlay overlay, Camera uiCamera);
        void Clear();
    }
}

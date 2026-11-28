using UI.Projection;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Screens.Windows
{
    /// <summary>
    /// A screen's two canvas roots. Its own GameObject is the ink root, which holds everything that is
    /// light and takes the clicks; <see cref="Under"/> holds the fade and the windows' dark bodies. Through
    /// the canopy the under root is a separate canvas on the gameplay camera, destroyed with the ink root;
    /// on an overlay canvas both are the same root.
    /// </summary>
    internal sealed class ScreenLayers : MonoBehaviour
    {
        public RectTransform Under { get; private set; }

        /// <summary>The canopy the screen draws through; null on an overlay canvas.</summary>
        public CanopyProjection Projection { get; private set; }

        public bool Canopy => Projection;

        internal ScreenCameras Cameras { get; private set; }

        internal static ScreenLayers Create(GameObject root, ScreenCameras cameras,
            CanvasScaler.ScreenMatchMode match = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight)
        {
            var layers = root.AddComponent<ScreenLayers>();
            var ink = ScreenKit.ScaledCanvas(root, match);
            root.AddComponent<GraphicRaycaster>();
            if (cameras == null)
            {
                layers.Under = (RectTransform)root.transform;
                return layers;
            }

            cameras.ConfigureInk(ink);
            var under = new GameObject(root.name + " Under", typeof(RectTransform));
            under.transform.SetParent(root.transform.parent, false);
            cameras.ConfigureUnder(ScreenKit.ScaledCanvas(under, match));
            layers.Under = (RectTransform)under.transform;
            layers.Projection = cameras.Projection;
            layers.Cameras = cameras;
            return layers;
        }

        private void OnDestroy()
        {
            if (Under && Under.gameObject != gameObject) Destroy(Under.gameObject);
        }
    }
}

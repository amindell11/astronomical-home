using Cameras;
using UnityEngine;

namespace Audio
{
    /// <summary>
    /// The camera's view center on the game plane: the camera's StudioListener
    /// attenuates from here (its attenuation object) and pans from <see cref="ListenerPanPoint"/>,
    /// which sits well off the plane, so distances to emitters on the plane are measured in-plane instead.
    /// </summary>
    public sealed class ListenerPlaneAnchor : MonoBehaviour
    {
        private Camera followed;

        private void Awake()
        {
            followed = GetComponentInParent<Camera>();
        }

        // After the camera has moved this frame; the listener reads the anchor next frame.
        private void LateUpdate() => transform.position =
            CameraUtils.GamePlanePoint(followed.ViewportPointToRay(new Vector3(0.5f, 0.5f)));
    }
}

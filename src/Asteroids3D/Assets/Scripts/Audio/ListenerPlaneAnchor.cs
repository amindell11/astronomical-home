using Substrate;
using UnityEngine;

namespace Audio
{
    /// <summary>
    /// The point on the game plane beneath its parent, the camera: the camera's StudioListener
    /// attenuates from here (its attenuation object) and pans from <see cref="ListenerPanPoint"/>,
    /// which sits well off the plane, so distances to emitters on the plane are measured in-plane instead.
    /// </summary>
    public sealed class ListenerPlaneAnchor : MonoBehaviour
    {
        private Transform followed;

        private void Awake()
        {
            followed = transform.parent;
        }

        // After the camera has moved this frame; the listener reads the anchor next frame.
        private void LateUpdate() => transform.position = GamePlane.ProjectOntoPlane(followed.position);
    }
}

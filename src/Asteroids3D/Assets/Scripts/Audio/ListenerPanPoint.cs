using Substrate;
using UnityEngine;

namespace Audio
{
    /// <summary>
    /// The point the camera's StudioListener sits on and pans from: above the plane point beneath the
    /// camera, at a height that scales with the orthographic zoom, so panning follows screen position.
    /// The camera's own height is fixed under zoom. Attenuation stays on <see cref="ListenerPlaneAnchor"/>.
    /// </summary>
    public sealed class ListenerPanPoint : MonoBehaviour
    {
        [Tooltip("Degrees off-centre a sound at the screen's side edge pans to, at every zoom. 90 is fully in one ear.")]
        [SerializeField, Range(5f, 85f)] private float edgeAngle = 30f;

        private Camera view;

        private void Awake()
        {
            view = GetComponentInParent<Camera>();
        }

        // After the camera has moved this frame; the listener reads the pan point next frame.
        private void LateUpdate()
        {
            var height = view.orthographicSize * view.aspect / Mathf.Tan(edgeAngle * Mathf.Deg2Rad);
            transform.position = GamePlane.ProjectOntoPlane(view.transform.position) - view.transform.forward * height;
        }
    }
}

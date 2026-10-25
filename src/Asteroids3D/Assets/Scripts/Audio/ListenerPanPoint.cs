using Cameras;
using UnityEngine;

namespace Audio
{
    /// <summary>
    /// Positions the StudioListener from the view's center and horizontal span on the game plane,
    /// keeping stereo width stable across projections. Attenuation stays on <see cref="ListenerPlaneAnchor"/>.
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
            var center = CameraUtils.GamePlanePoint(view.ViewportPointToRay(new Vector3(0.5f, 0.5f)));
            var edge = CameraUtils.GamePlanePoint(view.ViewportPointToRay(new Vector3(1f, 0.5f)));
            var height = Vector3.Distance(center, edge) / Mathf.Tan(edgeAngle * Mathf.Deg2Rad);
            transform.position = center - view.transform.forward * height;
        }
    }
}

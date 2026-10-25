using Substrate;
using UnityEngine;

namespace Cameras
{
    [RequireComponent(typeof(Camera))]
    public sealed class MinimapCam : MonoBehaviour
    {
        private Camera source;

        private void Awake() => source = transform.parent.GetComponent<Camera>();

        private void LateUpdate()
        {
            var center = CameraUtils.GamePlanePoint(source.ViewportPointToRay(new Vector3(0.5f, 0.5f)));
            transform.SetPositionAndRotation(center - GamePlane.Normal * 50f, GamePlane.Rotation);
        }
    }
}

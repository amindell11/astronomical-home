using Game;
using UnityEngine;

namespace Cameras
{
    [RequireComponent(typeof(Camera))]
    public abstract class SmoothCamera : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField]
        protected Vector3 offset = new Vector3(0, 0, -10f); // Offset in GamePlane basis (x=Right, y=Forward, z=Normal)

        [SerializeField, Min(0f)]
        protected float smoothTime = 0.15f;

        [Header("Zoom (Orthographic Size)")]
        [SerializeField] protected float minZoom = 5f;
        [SerializeField] protected float maxZoom = 50f;
        [SerializeField] protected float padding = 2f;

        protected Camera Cam { get; private set; }
        protected Vector3 dampVelocity;
        protected float zoomVelocity;

        protected virtual void Awake()
        {
            Cam = GetComponent<Camera>();
            Cam.orthographic = true;
        }
    
        protected virtual void Update()
        {
            ComputeDesiredCameraState(out var desiredCenter, out var desiredSize);
            Vector3? desiredPos = desiredCenter.HasValue ? ComputeWorldPosition(desiredCenter.Value) : null;
            ApplySmoothMovement(desiredPos, desiredSize);
            ApplyCameraOrientation();
        }

        protected abstract void ComputeDesiredCameraState(out Vector2? desiredCenter, out float? desiredSize);

        protected Vector3 ComputeWorldPosition(Vector2 center2D)
        {
            var worldCenter = GamePlane.PlanePointToWorld(center2D);
            var worldOffset = GamePlane.Right * offset.x 
                              + GamePlane.Forward * offset.y 
                              + GamePlane.Normal * offset.z;
            return worldCenter + worldOffset;
        }

        private void ApplySmoothMovement(Vector3? desiredPos, float? desiredSize)
        {
            if (desiredPos.HasValue)
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position, desiredPos.Value, ref dampVelocity, smoothTime,
                    float.PositiveInfinity, Time.unscaledDeltaTime);
            }

            if (desiredSize.HasValue)
            {
                Cam.orthographicSize = Mathf.SmoothDamp(
                    Cam.orthographicSize, desiredSize.Value, ref zoomVelocity, smoothTime,
                    float.PositiveInfinity, Time.unscaledDeltaTime);
            }
        }


        private void ApplyCameraOrientation()
        {
            transform.rotation = Quaternion.LookRotation(GamePlane.Normal, GamePlane.Forward);
        }

        protected float ClampZoom(float size) => Mathf.Clamp(size, minZoom, maxZoom);
    }
}

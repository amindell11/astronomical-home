using UnityEngine;
using Substrate;

namespace Cameras
{
    public enum FlightView { Original, TiltedOrthographic, TiltedPerspective }

    public abstract class SmoothCamera : CameraController
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

        protected Vector3 dampVelocity;
        protected float zoomVelocity;

        public FlightView View { get; private set; }
        private bool snapView;
        private const float TiltDegrees = 25f;
        private const float PerspectiveFieldOfView = 30f;

        public void CycleView()
        {
            View = (FlightView)(((int)View + 1) % 3);
            snapView = true;
        }

        protected override void Awake()
        {
            base.Awake();
            Cam.orthographic = true;
        }
    
        protected virtual void Update()
        {
            ComputeDesiredCameraState(out var desiredCenter, out var desiredSize);
            ApplyCameraOrientation();
            Cam.orthographic = View != FlightView.TiltedPerspective;
            if (!Cam.orthographic) Cam.fieldOfView = PerspectiveFieldOfView;
            if (snapView && desiredCenter.HasValue && desiredSize.HasValue)
            {
                Cam.orthographicSize = desiredSize.Value;
                transform.position = ComputeWorldPosition(desiredCenter.Value);
                dampVelocity = Vector3.zero;
                zoomVelocity = 0f;
                snapView = false;
            }
            Vector3? desiredPos = desiredCenter.HasValue ? ComputeWorldPosition(desiredCenter.Value) : null;
            ApplySmoothMovement(desiredPos, desiredSize);
        }

        protected abstract void ComputeDesiredCameraState(out Vector2? desiredCenter, out float? desiredSize);

        protected Vector3 ComputeWorldPosition(Vector2 center2D)
        {
            var worldCenter = GamePlane.PlanePointToWorld(center2D);
            if (View != FlightView.Original)
            {
                var distance = Cam.orthographic
                    ? Mathf.Abs(offset.z)
                    : Cam.orthographicSize / Mathf.Tan(PerspectiveFieldOfView * Mathf.Deg2Rad * 0.5f);
                return worldCenter - transform.forward * distance;
            }
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
            if (View != FlightView.Original)
                transform.rotation = Quaternion.AngleAxis(TiltDegrees, GamePlane.Right) * transform.rotation;
        }

        protected float ClampZoom(float size) => Mathf.Clamp(size, minZoom, maxZoom);
    }
}

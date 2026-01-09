using UnityEngine;
using Game;

[RequireComponent(typeof(Camera))]
public abstract class CameraFollowBase : MonoBehaviour
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
        if (!HasValidTargets()) return;

        ComputeDesiredCameraState(out var desiredPos, out var desiredSize);
        ApplySmoothMovement(desiredPos, desiredSize);
        ApplyCameraOrientation();
    }

    /// <summary>
    /// Template method that orchestrates the camera state computation.
    /// Subclasses can override individual steps.
    /// </summary>
    protected virtual void ComputeDesiredCameraState(out Vector3 desiredPos, out float desiredSize)
    {
        if (!TryGetPlaneBounds(out var min2D, out var max2D))
        {
            desiredPos = transform.position;
            desiredSize = Mathf.Clamp(Cam.orthographicSize, minZoom, maxZoom);
            return;
        }
        var center2D = ComputeFocusCenter(min2D, max2D);
        desiredSize = ComputeDesiredZoom(center2D, min2D, max2D);
        center2D = ApplyViewConstraints(center2D, desiredSize);
        desiredPos = ComputeWorldPosition(center2D);
    }
    
    protected abstract bool HasValidTargets();

    protected abstract bool TryGetPlaneBounds(out Vector2 min, out Vector2 max);

    protected abstract Vector2 ComputeFocusCenter(Vector2 boundsMin, Vector2 boundsMax);

    protected abstract float ComputeDesiredZoom(Vector2 center, Vector2 boundsMin, Vector2 boundsMax);

    protected virtual Vector2 ApplyViewConstraints(Vector2 center, float zoomSize) => center;

    private void GetPlaneBounds()
    {
        
    }

    protected Vector3 ComputeWorldPosition(Vector2 center2D)
    {
        var worldCenter = GamePlane.PlanePointToWorld(center2D);
        var worldOffset = GamePlane.Right * offset.x 
                        + GamePlane.Forward * offset.y 
                        + GamePlane.Normal * offset.z;
        return worldCenter + worldOffset;
    }

    protected void ApplySmoothMovement(Vector3 desiredPos, float desiredSize)
    {
        transform.position = Vector3.SmoothDamp(
            transform.position, desiredPos, ref dampVelocity, smoothTime,
            float.PositiveInfinity, Time.unscaledDeltaTime);

        Cam.orthographicSize = Mathf.SmoothDamp(
            Cam.orthographicSize, desiredSize, ref zoomVelocity, smoothTime,
            float.PositiveInfinity, Time.unscaledDeltaTime);
    }

    protected void ApplyCameraOrientation()
    {
        transform.rotation = Quaternion.LookRotation(GamePlane.Normal, GamePlane.Forward);
    }

    protected float ClampZoom(float size) => Mathf.Clamp(size, minZoom, maxZoom);
}

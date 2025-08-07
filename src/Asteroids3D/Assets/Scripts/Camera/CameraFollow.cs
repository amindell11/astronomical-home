using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Editor;
using Game;
using Utils;
using ShipMain;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("Targets & Layers")]
    [Tooltip("Layer that all controllable ships are assigned to.")]
    [SerializeField] private LayerMask shipLayer;

    [Header("Movement")]
    [SerializeField] private Vector3 offset = new Vector3(0, 0, -10f); // Offset expressed in GamePlane basis (x=Right, y=Forward, z=Normal)
    [SerializeField] [Min(0f)] private float smoothTime = 0.15f;       // Approx. time to reach target. Smaller is snappier.

    [SerializeField] private bool lockCameraToPlayer = false;
    [SerializeField] private bool lockZoomToPlayer = false;
    [SerializeField] private float lockZoomDistance = 10f;

    [Header("Zoom (Orthographic Size)")]
    [SerializeField] private float minZoom = 5f;
    [SerializeField] private float maxZoom = 50f;
    [SerializeField] private float padding = 2f; // Extra space around ships


    [Header("Performance")]
    [Tooltip("How often (seconds) to refresh the list of ship targets. 0 = every frame.")]
    [SerializeField] private float refreshInterval = 0.5f;

    [Header("Behavior")]
    [Tooltip("If true, camera will adjust to keep the player (tagged 'Player') within the view frustum.")]
    [SerializeField] protected bool keepPlayerInView = true;

    protected readonly List<Transform> _targets = new();
    protected Camera _cam;
    private float _refreshTimer;
    protected Transform _player;
    private Vector3 _dampVelocity;
    private float _zoomVelocity;

    // Added property to expose current lock state
    public bool LockCameraToPlayer => lockCameraToPlayer;

    protected virtual void Awake()
    {
        _cam = GetComponent<Camera>();

        if (!_cam.orthographic)
        {
            RLog.CoreWarning("CameraFollow works best with an orthographic Camera. Switching camera to orthographic mode.");
            _cam.orthographic = true;
        }

        RefreshTargets();
    }

    protected virtual void Start()
    {
        _player = GameObject.FindGameObjectWithTag(TagNames.Player).transform;
    }

    protected virtual void Update()
    {
        if (refreshInterval > 0f)
        {
            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= refreshInterval)
            {
                _refreshTimer = 0f;
                RefreshTargets();
            }
        }
        else
        {
            RefreshTargets();
        }
    }

    private void FixedUpdate()
    {
        if (_targets.Count == 0) return;
        // Calculate the target camera position & size based on current settings.
        ComputeDesiredCameraState(out Vector3 desiredPos, out float desiredSize);

        // Smooth movement & zoom to the desired state
        transform.position    = Vector3.SmoothDamp(transform.position, desiredPos, ref _dampVelocity, smoothTime, float.PositiveInfinity, Time.unscaledDeltaTime);
        _cam.orthographicSize = Mathf.SmoothDamp(_cam.orthographicSize, desiredSize, ref _zoomVelocity, smoothTime, float.PositiveInfinity, Time.unscaledDeltaTime);

        // Ensure camera orientation follows the game plane
        transform.rotation = Quaternion.LookRotation(GamePlane.Normal, GamePlane.Forward);
    }

    // NEW: Centralized computation of desired camera position & zoom considering lock flags
    private void ComputeDesiredCameraState(out Vector3 desiredPos, out float desiredSize)
    {
        // -----------------------------------------------------------------
        // 1. Compute bounds in plane space for ALL targets (needed for zoom)
        GetPlaneBounds(out Vector2 min2D, out Vector2 max2D);
        Vector2 boundsCenter2D = (min2D + max2D) * 0.5f;

        // -----------------------------------------------------------------
        // 2. Determine the 2-D center to focus on
        Vector2 center2D;
        if (lockCameraToPlayer && _player != null)
        {
            // Focus exclusively on the player
            center2D = GamePlane.WorldPointToPlane(_player.position);
        }
        else
        {
            // Use overall bounds center so all targets stay in view
            center2D = boundsCenter2D;
        }

        // -----------------------------------------------------------------
        // 3. Compute desired orthographic size (zoom)
        if (lockZoomToPlayer)
        {
            // Fixed size
            desiredSize = Mathf.Clamp(lockZoomDistance, minZoom, maxZoom);
        }
        else
        {
            // Compute horizontal & vertical extents from the chosen center
            float maxDX = Mathf.Max(center2D.x - min2D.x, max2D.x - center2D.x);
            float maxDY = Mathf.Max(center2D.y - min2D.y, max2D.y - center2D.y);

            float preferredSize = Mathf.Max(maxDY + padding, (maxDX + padding) / _cam.aspect);
            desiredSize         = Mathf.Clamp(preferredSize, minZoom, maxZoom);
        }

        // -----------------------------------------------------------------
        // 4. Optionally keep player fully inside view if camera is NOT locked to player
        if (!lockCameraToPlayer && keepPlayerInView && _player != null)
        {
            float horizontalExtent = desiredSize * _cam.aspect;
            float verticalExtent   = desiredSize;

            Vector3 tempWorldCenter = GamePlane.PlanePointToWorld(center2D);
            Vector3 toPlayerWorld = _player.position - tempWorldCenter;
            Vector2 toPlayer2D    = new Vector2(Vector3.Dot(toPlayerWorld, GamePlane.Right),
                                                Vector3.Dot(toPlayerWorld, GamePlane.Forward));

            if (Mathf.Abs(toPlayer2D.x) > horizontalExtent - padding)
            {
                float shiftX = Mathf.Abs(toPlayer2D.x) - (horizontalExtent - padding);
                center2D += new Vector2(Mathf.Sign(toPlayer2D.x) * shiftX, 0f);
            }

            if (Mathf.Abs(toPlayer2D.y) > verticalExtent - padding)
            {
                float shiftY = Mathf.Abs(toPlayer2D.y) - (verticalExtent - padding);
                center2D += new Vector2(0f, Mathf.Sign(toPlayer2D.y) * shiftY);
            }
        }

        // -----------------------------------------------------------------
        // 5. Convert center back to world space and apply the configured offset
        Vector3 worldCenter = GamePlane.PlanePointToWorld(center2D);
        Vector3 worldOffset = GamePlane.Right * offset.x + GamePlane.Forward * offset.y + GamePlane.Normal * offset.z;

        desiredPos = worldCenter + worldOffset;
    }

    // ---------------------------------------------------------------------
    // Helper: compute min/max bounds in plane coordinates
    void GetPlaneBounds(out Vector2 min, out Vector2 max)
    {
        min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        foreach (var t in _targets)
        {
            Vector2 p = GamePlane.WorldPointToPlane(t.position);
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
    }

    // Fallback world-space bounds (XY) used only for editor gizmos
    private Bounds GetTargetsBounds()
    {
        Bounds bounds = new Bounds(_targets[0].position, Vector3.zero);
        RLog.Core("Camera Bounds targets:" +_targets.Count);
        for (int i = 1; i < _targets.Count; i++)
        {
            bounds.Encapsulate(_targets[i].position);
        }
        return bounds;
    }

    // Refresh the list of ship targets using the configured LayerMask
    protected virtual void RefreshTargets()
    {
        _targets.Clear();

        int layerMask = shipLayer.value;
        foreach (Transform t in Ship.ActiveShips)
        {
            if (t == null) continue;
            if (((1 << t.gameObject.layer) & layerMask) != 0)
            {
                _targets.Add(t);
            }
        }
    }

    public void SetLockCameraToPlayer(bool value)
    {
        lockCameraToPlayer = value;
    }

    public void SetLockZoomToPlayer(bool value)
    {
        lockZoomToPlayer = value;
    }
    
    

#if UNITY_EDITOR
    // Draw a gizmo representing the bounding box for debugging
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || _targets.Count == 0) return;
        Gizmos.color = Color.yellow;
        Bounds b = GetTargetsBounds();
        Gizmos.DrawWireCube(b.center, b.size);
    }
#endif
}
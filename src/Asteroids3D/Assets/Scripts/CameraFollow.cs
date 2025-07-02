using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("Targets & Layers")]
    [Tooltip("Layer that all controllable ships are assigned to.")]
    [SerializeField] private LayerMask shipLayer;

    [Header("Movement")]
    [SerializeField] private Vector3 offset = new Vector3(0, 0, -10f); // Offset expressed in GamePlane basis (x=Right, y=Forward, z=Normal)
    [SerializeField] [Min(0f)] private float smoothSpeed = 5f;         // Higher is snappier

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

    protected virtual void Awake()
    {
        _cam = GetComponent<Camera>();

        if (keepPlayerInView)
        {
            // Cache player transform for quick access (may be null if not found)
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                _player = playerObj.transform;
            }
        }

        if (!_cam.orthographic)
        {
            RLog.CoreWarning("CameraFollow works best with an orthographic Camera. Switching camera to orthographic mode.");
            _cam.orthographic = true;
        }

        RefreshTargets();
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

    private void LateUpdate()
    {
        if (_targets.Count == 0) return;

        // -----------------------------------------------------------------
        // 1. Compute bounds in GamePlane space (Right = X, Forward = Y)
        GetPlaneBounds(out Vector2 min2D, out Vector2 max2D);

        Vector2 center2D = (min2D + max2D) * 0.5f;
        float   width    = max2D.x - min2D.x;
        float   height   = max2D.y - min2D.y;

        // 2. Determine required orthographic size
        float preferredSize = Mathf.Max(height * 0.5f, width * 0.5f / _cam.aspect) + padding;
        float clampedSize   = Mathf.Clamp(preferredSize, minZoom, maxZoom);
        float newSize       = Mathf.Lerp(_cam.orthographicSize, clampedSize, smoothSpeed * Time.unscaledDeltaTime);

        // 3. Desired camera position (center of targets + offset expressed in plane basis)
        Vector3 worldCenter  = GamePlane.PlaneToWorld(center2D);
        Vector3 worldOffset  = GamePlane.Right * offset.x + GamePlane.Forward * offset.y + GamePlane.Normal * offset.z;
        Vector3 desiredPos   = worldCenter + worldOffset;

        // 4. Keep player within view (operate in plane space)
        if (keepPlayerInView && _player != null)
        {
            float horizontalExtent = newSize * _cam.aspect;
            float verticalExtent   = newSize;

            Vector3 toPlayerWorld  = _player.position - desiredPos;
            Vector2 toPlayer2D     = new Vector2(Vector3.Dot(toPlayerWorld, GamePlane.Right),
                                                 Vector3.Dot(toPlayerWorld, GamePlane.Forward));

            if (Mathf.Abs(toPlayer2D.x) > horizontalExtent - padding)
            {
                float shiftX = Mathf.Abs(toPlayer2D.x) - (horizontalExtent - padding);
                desiredPos += GamePlane.Right * Mathf.Sign(toPlayer2D.x) * shiftX;
            }

            if (Mathf.Abs(toPlayer2D.y) > verticalExtent - padding)
            {
                float shiftY = Mathf.Abs(toPlayer2D.y) - (verticalExtent - padding);
                desiredPos += GamePlane.Forward * Mathf.Sign(toPlayer2D.y) * shiftY;
            }
        }

        // 5. Smooth movement & zoom
        transform.position      = Vector3.Lerp(transform.position, desiredPos, smoothSpeed * Time.unscaledDeltaTime);
        _cam.orthographicSize   = newSize;

        // 6. Ensure camera orientation follows the plane
        transform.rotation = Quaternion.LookRotation(GamePlane.Normal, GamePlane.Forward);
    }

    // ---------------------------------------------------------------------
    // Helper: compute min/max bounds in plane coordinates
    void GetPlaneBounds(out Vector2 min, out Vector2 max)
    {
        min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        foreach (var t in _targets)
        {
            Vector2 p = GamePlane.WorldToPlane(t.position);
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
using System;
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
    [Header("Movement")] [SerializeField]
    private Vector3
        offset = new Vector3(0, 0, -10f); // Offset expressed in GamePlane basis (x=Right, y=Forward, z=Normal)

    [SerializeField] [Min(0f)] private float smoothTime = 0.15f; // Approx. time to reach target. Smaller is snappier.

    [SerializeField] private bool lockCameraToPlayer = false;
    [SerializeField] private bool lockZoomToPlayer = false;
    [SerializeField] private float lockZoomDistance = 10f;

    [Header("Zoom (Orthographic Size)")] [SerializeField]
    private float minZoom = 5f;

    [SerializeField] private float maxZoom = 50f;
    [SerializeField] private float padding = 2f; // Extra space around ships
    
    [Header("Behavior")]
    [Tooltip("If true, camera will adjust to keep the player (tagged 'Player') within the view frustum.")]
    [SerializeField]
    protected bool keepPlayerInView = true;

    private HashSet<Transform> _targets;
    private Camera _cam;
    private Transform _player;
    private Vector3 _dampVelocity;
    private float _zoomVelocity;

    public void SetTargetSource<T>(SubscribedSet<T> set) where T : MonoBehaviour
    {
        _targets = set.Select(s => s.transform).ToHashSet();
        set.OnAdd += t => _targets.Add(t.transform);
        set.OnRemove += t => _targets.Remove(t.transform);
    }

    public void SetPlayer<T>(T player) where T : MonoBehaviour
    {
        _player = player.transform;
    }

    public bool LockCameraToPlayer => lockCameraToPlayer;

    protected virtual void Awake()
    {
        _cam = GetComponent<Camera>();
        _cam.orthographic = true;
    }

    protected virtual void Start()
    {
        _player = GameObject.FindGameObjectWithTag(TagNames.Player).transform;
    }

    private void FixedUpdate()
    {
        if (_targets == null || _targets.Count == 0) return;
        // Calculate the target camera position & size based on current settings.
        ComputeDesiredCameraState(out var desiredPos, out float desiredSize);

        // Smooth movement & zoom to the desired state
        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _dampVelocity, smoothTime,
            float.PositiveInfinity, Time.unscaledDeltaTime);
        _cam.orthographicSize = Mathf.SmoothDamp(_cam.orthographicSize, desiredSize, ref _zoomVelocity, smoothTime,
            float.PositiveInfinity, Time.unscaledDeltaTime);

        // Ensure camera orientation follows the game plane
        transform.rotation = Quaternion.LookRotation(GamePlane.Normal, GamePlane.Forward);
    }

    private void ComputeDesiredCameraState(out Vector3 desiredPos, out float desiredSize)
    {
        // -----------------------------------------------------------------
        // 1. Compute bounds in plane space for ALL targets (needed for zoom)
        if (!TryGetPlaneBounds(out var min2D, out var max2D))
        {
            // No active targets found; keep current camera state
            desiredPos = transform.position;
            desiredSize = Mathf.Clamp(_cam.orthographicSize, minZoom, maxZoom);
            return;
        }
        var boundsCenter2D = (min2D + max2D) * 0.5f;

        // -----------------------------------------------------------------
        // 2. Determine the 2-D center to focus on
        Vector2 center2D;
        if (lockCameraToPlayer && _player)
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
            float maxDx = Mathf.Max(center2D.x - min2D.x, max2D.x - center2D.x);
            float maxDy = Mathf.Max(center2D.y - min2D.y, max2D.y - center2D.y);

            float preferredSize = Mathf.Max(maxDy + padding, (maxDx + padding) / _cam.aspect);
            desiredSize = Mathf.Clamp(preferredSize, minZoom, maxZoom);
        }

        // -----------------------------------------------------------------
        // 4. Optionally keep player fully inside view if camera is NOT locked to player
        if (!lockCameraToPlayer && keepPlayerInView && _player != null)
        {
            float horizontalExtent = desiredSize * _cam.aspect;
            float verticalExtent = desiredSize;

            var tempWorldCenter = GamePlane.PlanePointToWorld(center2D);
            var toPlayerWorld = _player.position - tempWorldCenter;
            var toPlayer2D = new Vector2(Vector3.Dot(toPlayerWorld, GamePlane.Right),
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
        var worldCenter = GamePlane.PlanePointToWorld(center2D);
        var worldOffset = GamePlane.Right * offset.x + GamePlane.Forward * offset.y + GamePlane.Normal * offset.z;

        desiredPos = worldCenter + worldOffset;
    }

    private bool TryGetPlaneBounds(out Vector2 min, out Vector2 max)
    {
        min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        bool foundAny = false;
        foreach (var t in _targets)
        {
            var go = t.gameObject;
            if (!go || !go.activeInHierarchy) continue;
            var p = GamePlane.WorldPointToPlane(t.position);
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
            foundAny = true;
        }
        if (foundAny) return true;
        min = Vector2.zero;
        max = Vector2.zero;
        return false;
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
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || _targets == null || _targets.Count == 0) return;
        if (!TryGetPlaneBounds(out var min2D, out var max2D)) return;

        var p00 = GamePlane.PlanePointToWorld(new Vector2(min2D.x, min2D.y));
        var p01 = GamePlane.PlanePointToWorld(new Vector2(min2D.x, max2D.y));
        var p11 = GamePlane.PlanePointToWorld(new Vector2(max2D.x, max2D.y));
        var p10 = GamePlane.PlanePointToWorld(new Vector2(max2D.x, min2D.y));

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(p00, p01);
        Gizmos.DrawLine(p01, p11);
        Gizmos.DrawLine(p11, p10);
        Gizmos.DrawLine(p10, p00);
    }
#endif
}
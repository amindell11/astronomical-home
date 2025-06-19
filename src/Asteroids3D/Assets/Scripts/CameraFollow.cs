using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("Targets & Layers")]
    [Tooltip("Layer that all controllable ships are assigned to.")]
    [SerializeField] private LayerMask shipLayer;

    [Header("Movement")]
    [SerializeField] private Vector3 offset = new Vector3(0, 0, -10f); // Keep camera back on Z axis
    [SerializeField] [Min(0f)] private float smoothSpeed = 5f;         // Higher is snappier

    [Header("Zoom (Orthographic Size)")]
    [SerializeField] private float minZoom = 5f;
    [SerializeField] private float maxZoom = 50f;
    [SerializeField] private float padding = 2f; // Extra space around ships

    [Header("Performance")]
    [Tooltip("How often (seconds) to refresh the list of ship targets. 0 = every frame.")]
    [SerializeField] private float refreshInterval = 0.5f;

    private readonly List<Transform> _targets = new();
    private Camera _cam;
    private float _refreshTimer;
    private Transform _player;

    private void Awake()
    {
        _cam = GetComponent<Camera>();

        // Cache player transform for quick access (may be null if not found)
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _player = playerObj.transform;
        }

        if (!_cam.orthographic)
        {
            Debug.LogWarning("CameraFollow works best with an orthographic Camera. Switching camera to orthographic mode.");
            _cam.orthographic = true;
        }

        RefreshTargets();
    }

    private void Update()
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
        if (_targets.Count == 0)
        {
            return;
        }

        // --- Position & Zoom ---
        Bounds bounds = GetTargetsBounds();

        // Calculate the target orthographic size needed to fit all ships (before clamping)
        float preferredSize = Mathf.Max(bounds.size.y * 0.5f, bounds.size.x * 0.5f / _cam.aspect) + padding;
        float clampedSize = Mathf.Clamp(preferredSize, minZoom, maxZoom);

        // Smooth zoom towards clamped target size
        float newSize = Mathf.Lerp(_cam.orthographicSize, clampedSize, smoothSpeed * Time.unscaledDeltaTime);

        // Start with centering on all ships
        Vector3 desiredPosition = bounds.center + offset;

        // Ensure player remains within view if present
        if (_player != null)
        {
            float horizontalExtent = newSize * _cam.aspect;
            float verticalExtent = newSize;

            Vector3 toPlayer = _player.position - desiredPosition;

            // Shift along X if needed
            if (Mathf.Abs(toPlayer.x) > horizontalExtent - padding)
            {
                float shiftX = Mathf.Abs(toPlayer.x) - (horizontalExtent - padding);
                desiredPosition.x += Mathf.Sign(toPlayer.x) * shiftX;
            }

            // Shift along Y if needed
            if (Mathf.Abs(toPlayer.y) > verticalExtent - padding)
            {
                float shiftY = Mathf.Abs(toPlayer.y) - (verticalExtent - padding);
                desiredPosition.y += Mathf.Sign(toPlayer.y) * shiftY;
            }
        }

        // Smoothly move camera towards the final desired position
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.unscaledDeltaTime);

        // Apply the new zoom
        _cam.orthographicSize = newSize;
    }

    // Calculates an encapsulating bounds around all target transforms (in X/Y plane)
    private Bounds GetTargetsBounds()
    {
        Bounds bounds = new Bounds(_targets[0].position, Vector3.zero);
        for (int i = 1; i < _targets.Count; i++)
        {
            bounds.Encapsulate(_targets[i].position);
        }
        return bounds;
    }

    // Refresh the list of ship targets using the configured LayerMask
    private void RefreshTargets()
    {
        _targets.Clear();

        // Efficiently gather all root Transforms, then filter by layer.
        // Using FindObjectsOfType<Transform> is acceptable at small scale and with refreshInterval.
        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        int layerMask = shipLayer.value;
        foreach (Transform t in allTransforms)
        {
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
using UnityEngine;

/// <summary>
/// Simple billboard behaviour that keeps the GameObject facing the main camera.
/// Attach to the root of a world-space canvas so it always looks flat to the player.
/// </summary>
public sealed class Billboard : MonoBehaviour
{
    private Camera mainCam;

    void Awake()
    {
        mainCam = Camera.main;
    }

    void LateUpdate()
    {
        if (mainCam == null) return;
        // Make forward vector match camera's forward so the canvas looks head-on
        transform.forward = mainCam.transform.forward;
    }
} 
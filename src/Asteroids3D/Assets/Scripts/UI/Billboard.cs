using UnityEngine;

/// <summary>
/// Simple billboard behaviour that keeps the GameObject facing the main camera.
/// Attach to the root of a world-space canvas so it always looks flat to the player.
/// </summary>
public sealed class Billboard : MonoBehaviour
{
    void LateUpdate()
    {
        transform.up = -GamePlane.Normal;
    }
} 
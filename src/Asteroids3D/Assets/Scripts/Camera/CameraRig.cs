using UnityEngine;

/// <summary>
/// Holds references to cameras in a camera rig hierarchy.
/// Attach to the root camera object and wire up child cameras in the inspector.
/// </summary>
public class CameraRig : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera uiCamera;
    public CameraFollow CameraFollow { get; private set; }

    private void Awake()
    {
        CameraFollow = GetComponent<CameraFollow>();
    }
    
    public Camera MainCamera => mainCamera;
    public Camera UICamera => uiCamera;
}

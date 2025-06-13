using UnityEngine;

// Translates player input into commands for the Ship component.
[RequireComponent(typeof(Ship))]
public class PlayerShipInput : MonoBehaviour
{
    private Ship ship;
    private Camera mainCamera;

    private void Start()
    {
        ship = GetComponent<Ship>();
        mainCamera = Camera.main;
    }

    private void Update()
    {
        // Read movement inputs
        float thrustInput = Input.GetAxis("Vertical");
        float strafeInput = Input.GetAxis("Horizontal");
        ship.SetControls(thrustInput, strafeInput);
        
        // Read rotation input
        bool wantsToRotate = Input.GetButton("Direction");
        Vector3 mousePos = Vector3.zero;
        if (wantsToRotate)
        {
            mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        }
        ship.SetRotationTarget(wantsToRotate, mousePos);
    }
}
using UnityEngine;

/// Attach to an empty GameObject.  
/// Every frame it snaps to the point where the mouse ray
/// hits an X-Z plane at y = targetPlaneY (default 0).
public class MouseFollower : MonoBehaviour
{
    public float targetPlaneY = 0f;   // height of navigation plane

    Camera cam;
    Plane  navPlane;

    void Awake()
    {
        cam      = Camera.main;
        navPlane = new Plane(Vector3.up, new Vector3(0, targetPlaneY, 0));
    }

    void Update()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (navPlane.Raycast(ray, out float enter))
            transform.position = ray.GetPoint(enter);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(transform.position, 0.4f);
    }
#endif
}

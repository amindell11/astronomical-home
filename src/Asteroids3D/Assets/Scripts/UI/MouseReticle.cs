using UnityEngine;

namespace UI
{
    /// <summary>
    /// Positions a reticle GameObject so that it tracks the current mouse position.
    /// If <see cref="useWorldSpace"/> is <c>false</c> (default), the reticle should live
    /// under a Screen-Space canvas: the script simply sets its world-position to
    /// <see cref="Input.mousePosition"/> each frame.
    /// If <c>true</c>, the script projects the mouse ray onto the <see cref="GamePlane"/>
    /// (defined elsewhere in the project) so the reticle sits in the 3-D world at
    /// the point the player is "aiming".  Pair this with a world-space canvas & a
    /// <see cref="Billboard"/> component so the graphic always faces the camera.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class MouseReticle : MonoBehaviour
    {
        [Tooltip("If true, the reticle is positioned in 3-D space on the GamePlane.\n"+
                 "If false, reticle lives in screen-space and follows raw mouse pixels.")]
        [SerializeField] private bool useWorldSpace = false;

        [Tooltip("Offset along the GamePlane normal when in world-space mode.")]
        [SerializeField] private float worldSpaceOffset = 0.1f;

        Camera cam;
        RectTransform rect;

        void Awake()
        {
            cam  = Camera.main;
            rect = GetComponent<RectTransform>();
        }

        void Update()
        {
            if (useWorldSpace)
            {
                if (cam == null)
                {
                    cam = Camera.main;
                    if (cam == null) return; // cannot compute without a camera
                }

                // Build a ray from the camera through the mouse position.
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);

                // Define the plane using cached GamePlane data.
                Plane plane = new Plane(GamePlane.Normal, GamePlane.Origin + GamePlane.Normal * worldSpaceOffset);

                if (plane.Raycast(ray, out float enter))
                {
                    // Position reticle at intersection point.
                    transform.position = ray.GetPoint(enter);
                }
            }
            else
            {
                // Screen-space canvas: just match mouse pixels.
                rect.position = Input.mousePosition;
            }
        }
    }
} 